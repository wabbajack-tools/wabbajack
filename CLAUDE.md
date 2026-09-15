# Wabbajack

An automated modlist installer. It reproduces a modding setup on another machine by recording where every
file came from, then downloading and rebuilding it, without redistributing any mods.

Two things ship: a WPF desktop app (`Wabbajack.App.Wpf`) and a CLI (`Wabbajack.CLI`). The ~60 remaining
projects are libraries behind them.

## Build and test

```bash
dotnet build Wabbajack.sln -c Debug          # ~60s clean, expected to be warning-free
dotnet test                                  # whole suite
dotnet test Wabbajack.Paths.IO.Test          # one project
dotnet test --filter "FullyQualifiedName~FileDeletionRulesTests"
```

Offline lane, and what CI runs on pull requests:

```bash
dotnet test --filter "Category!=RequiresOAuth&Category!=RequiresNetwork"
```

Tests that reach the network or need a login carry `[Trait("Category", "RequiresNetwork")]` or
`[Trait("Category", "RequiresOAuth")]`. Add the trait when you write a test that needs either, so the
offline lane stays trustworthy.

Building from Linux or macOS needs `/p:EnableWindowsTargeting=true`, which is what the CI workflow passes.

### Credentials

`NEXUS_API_KEY` is read straight from the environment by `NexusApi.cs`. With it set, the `RequiresOAuth`
tests run. Any `EncryptedJsonTokenProvider<T>` falls back to an environment variable named after its key
uppercased with dashes turned into underscores, so `discord-endpoints` reads `DISCORD_ENDPOINTS`. Stored tokens live in
`%LOCALAPPDATA%\Wabbajack\encrypted\`.

## Layout

Projects are small and single-purpose. The name says what is inside.

| Area | Projects |
|---|---|
| Paths and IO | `Wabbajack.Paths`, `Wabbajack.Paths.IO`, `Wabbajack.IO.Async` |
| Core flows | `Wabbajack.Compiler`, `Wabbajack.Installer`, `Wabbajack.VFS` |
| Archives | `Wabbajack.Compression.BSA`, `Wabbajack.Compression.Zip`, `Wabbajack.FileExtractor` |
| Downloads | `Wabbajack.Downloaders.*`, dispatched by `Wabbajack.Downloaders.Dispatcher`; `Wabbajack.Downloaders.ManualSources` holds metadata-only downloaders (`Resolve`/`MetaIni`/`Parse`) for sources the user fetches in a browser |
| Preflight | `Wabbajack.Installer/Preflight` — the checks that run before an install (see below) |
| Network clients | `Wabbajack.Networking.*` |
| Serialization | `Wabbajack.DTOs` plus the `Wabbajack.DTOs.ConverterGenerators` source generator |
| Wiring | `Wabbajack.Services.OSIntegrated` registers nearly everything in DI |
| UI | `Wabbajack.App.Wpf` (ReactiveUI), `Wabbajack.Launcher` |

Packages are pinned centrally in `Directory.Packages.props`. A `<PackageReference>` carries no version.

**FluentAssertions stays on 7.2.2 permanently.** 8.0 left Apache 2.0 for a paid Xceed licence. Do not
upgrade it, and do not add it to a project that does not already use it.

**SteamKit2 is LGPL-2.1-only, and that was accepted deliberately.** It is the only realistic way to talk to
Steam, and LGPL is satisfied by unmodified dynamic linking, which is exactly what a `PackageReference` from
NuGet does. So: keep it as an ordinary pinned `PackageReference`, never vendor or patch the source, and keep
its types inside `Wabbajack.Networking.Steam` rather than letting them spread. This is the one copyleft
dependency in the tree; adding another is a fresh decision, not a precedent.

Logs are written to a `logs` folder next to the executable, configured in `App.xaml.cs`.

## Conventions

- Paths use `AbsolutePath` and `RelativePath` rather than `string`. Build them with `.ToAbsolutePath()`,
  `.ToRelativePath()` and `.Combine(...)`, and compare with `==`, `InFolder` and `RelativeTo`.
- Parallel work goes through the `PDoAll` / `PMapAll` / `PMapAllBatched` extensions in
  `Wabbajack.Common/AsyncParallelExtensions.cs`, paired with an `IResource<T>` limiter so the user's thread
  and throughput settings are respected.
- Temporary files come from `TemporaryFileManager`. Its names come from `RandomName`.
- Most projects set `<Nullable>enable</Nullable>` while suppressing several nullable warnings via `NoWarn`.
- Files are CRLF. Some shell tools rewrite a whole file to LF, which buries a small change in a whole-file
  diff, so check `git diff --stat` after editing outside an editor.

## Things worth knowing before changing them

**Deleting the user's files.** `FileDeletionRules.ShouldDeleteExistingFile` decides which existing files an
install may delete, and `AInstaller.OptimizeModlist` acts on the answer. Several reports of lost saves and
wiped folders come back to this. The rules are pure and covered by `FileDeletionRulesTests`; keep them that
way and add a case for any change.

**`AbsolutePath` comparison is case-insensitive.** `Equals` uses `InvariantCultureIgnoreCase` while
`GetHashCode` uses `CurrentCultureIgnoreCase`. Anything generating unique path names should stay within one
case, which is why `RandomName` uses a single-case alphabet.

**Concurrency is the default.** The compiler, installer, extractor and hasher all run wide. A helper that
holds shared mutable state, or a library that does so internally, will be called from many threads at once.
`shortid` was replaced for exactly this reason: it repeated IDs under `Parallel.For`, which handed two
callers the same temporary path.

**Parallelism scales off `Environment.ProcessorCount`** with no memory awareness, so a high core count
machine can start far more work than it has RAM or pagefile for.

**`FileHashCache` decides a stored hash is still valid from the file's timestamp and size**, without
reading it. Damage that changes neither is still missed, which is the limit rather than an oversight:
catching it would mean rereading every file on every run. Rows written before the size column existed hold
NULL, are trusted as they always were, and gain a size the first time they are read, so no existing user is
made to rehash a downloads folder.

## Preflight

`Wabbajack.Installer/Preflight` runs a checklist before an install starts: game installed (100), archive
inventory (300), game files (350), unsupported archives (400), Nexus login and premium status (500), manual
downloads (600), automated downloads (700), disk space (900).
`PreflightRunner.Create(services, config)` mirrors `StandardInstaller.Create`; checks come from
DI as `IPreflightCheck` and run in `Order` (100–900, gaps left on purpose), each declaring `DependsOn`. A
dependent whose dependency has not passed is marked `Skipped` when its turn comes; everything the halt rule
below never reached stays `Pending`. `RunCheck(id)` re-runs one and resets what depends on it. The engine
has no UI and no DynamicData anywhere. The runner is event-based: it raises
plain events (`CheckChanged`, `ArchiveChanged`, `ManualQueueChanged`, `RunFinished`) and offers snapshots;
hosts project those however they like. The acquirer exposes `IObservable`s via `System.Reactive` (already
a transitive dependency).

**A run stops at the first check the user has to act on.** `PreflightRunner.StopsRun` is the rule: `Failed`
always stops; `NeedsUser` stops unless the check sets `IPreflightCheck.NeedsUserStopsRun` to false; nothing
else does. Past a failure the checklist is describing an install the user is not going to get, and the work
it does to say so — hashing a game folder, walking a downloads folder, downloading archives — is work they
then watch happen twice. The two download checks are the exceptions: `NeedsUser` is their ordinary ending
(the user has files to fetch, which is work rather than a mistake), so the run carries on to disk-space. A
`Warning` never stops a run, acknowledged or not — the user decides, and should decide with the whole
checklist in front of them. What did not run stays `Pending`, not `Skipped`: "Skipped because X did not
pass" belongs to a dependent. Every way back in — retry, rescan, acknowledge, pick a game folder — ends in
`RunAll`, which picks the `Pending` ones up where the stopped run left them.

Because a halt is that blunt, **a check that stops a run has to ask about work the user actually has left.**
nexus-login is why the rule reads that way: at Order 100 with no dependencies it stopped an un-logged-in
user at the first row, before they had learned whether their game was installed or what was already on
disk, and its "N files" came from every Nexus archive in the modlist rather than the missing ones — so a
user whose downloads folder was already complete was halted over files nobody was going to fetch. It now
runs at 500, after archive-inventory and unsupported-archives, and asks about `Missing`: no missing Nexus
archive means "not needed" and the run carries on. The download checks still depend on it.

**game-files was moved for the same reason, and answers the same question the installer does.** At 200 it
ran ahead of the pruning at 300, so it answered for every `GameFileSource` in the modlist rather than the
ones this install will read, and it resolved by path — `state.GameFile.RelativeTo(root)` plus a hash — which
is a question the installer never asks. `AInstaller.HashArchives` calls `ArchiveInventory.Scan` to flatten
the downloads folder and the game folders into one content-addressed map and tests
`ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash))`; `GameFileSource.GameFile` is never
dereferenced. So a correct copy of a game file anywhere the installer looks satisfies the install, and the
check could fail a run that would have succeeded. It now runs at 350, reads the map archive-inventory has
already built, and hashes nothing itself. The *missing* versus *mismatched* distinction is still decided by
the file's own path, because that is what tells the repair below whether today's build will do.

**Fetching game files is optional, and never writes to the game folder.** `Rules/GameFileRepair` takes what
game-files put on the blackboard and asks `IGameFileRestorer` — declared in `Wabbajack.Downloaders.GameFile`,
beside `GameFileDownloader` and `IGameLocator` — for "this file of this game at this version". That seam
names nothing of Steam, so `Wabbajack.Installer` never sees SteamKit; `SteamGameFileRestorer` in
`Wabbajack.Networking.Steam` is the only place that turns the question into an app, a depot and a manifest,
resolving a version through `indexed-game-files` and a missing file through whatever the app publishes
today. Fetched files go to the **downloads folder**, which is all the install needs and means no elevation,
nothing for Steam to re-patch, a game that still runs and an undo that is deleting a file. Repairing the
game install itself is a non-goal. Two hashes are checked: the restorer's, which proves the depot handed
over what it meant to, and `Archive.Hash`, which is the only thing that proves it is the file *this* install
needs — a file that fails it is deleted. The whole thing is opt-in: game-files offers
`PreflightAction.RepairGameFiles` only when the game came from Steam, a user with no login is told what one
would buy rather than having it happen to them, and a host that registers no `IGameFileRestorer` behaves
exactly as before. The CLI drives it with `repair-game-files`.

**The Creation Kit is not a game, and does not need to be one.** Steam gives it an app of its own —
1946180 for Skyrim SE, 1946160 for Fallout 4, 202480 for Skyrim, 2722710 for Starfield — with its own
depots, but its `installdir` is the game's and Skyrim SE's even declares `sharesdirwithapp 489830`. So
`CreationKit.exe`, `Data\Scripts.zip` and `Papyrus Compiler\PapyrusCompiler.exe` land beside `SkyrimSE.exe`,
`hash-game-files` records them relative to the game folder like anything else there, and a modlist carries
them as `GameFileSource { Game = SkyrimSpecialEdition }`. Nothing distinguishes them but the depot they have
to come from. That is `GameMetaData.SteamToolIDs`: no new `Game` member, no second game folder, just a
second app for `SteamGameFileRestorer` to search after the game's own. Tool apps are searched on the
current-build path only — `indexed-game-files` records depot and manifest ids with no app beside them, so an
id out of it can only be asked for under the game's app — which costs nothing, because a missing file tries
today's build first and a mismatched one falls back to it. They stay out of `SteamIDs`, which `GameLocator`
walks for an install the tool does not have.

**The Kit is free but not licence-free, so fetching from it adds a licence to the user's account.**
`common/FreeToDownload` is false: an account holding no package that names app 1946180 is refused the PICS
access token, so the app cannot even be described, let alone read. Bethesda's own licence is a no-cost
package granting exactly that app, and `ISteamContentClient.EnsureFreeLicenseAsync` asks for one the way
pressing Install on a free store page does. Skyrim is the exception that proves the shape: 202480 is granted
by the same packages that grant 72850, so owning Skyrim is owning its Kit.

That request is the only thing in the tree that writes to a user's Steam account, so three rules hold it
down. It is asked **lazily** — at the point the tool app's depots are about to be read, which the restorer
reaches only after the game's own depots were searched and did not carry the file, so a list missing a DLC
never adds a Creation Kit. It is **never asked on an unconfirmed read**: `DepotEntitlement.DecideFreeLicense`
is the rule, and a licence list that never arrived is `Unconfirmed`, not "owns nothing" — the same refusal to
guess `CheckAccessAsync` makes, pointed at the case where guessing wrong puts a package in somebody's
library. And the answer is **memoised for the run, negative as well as positive**, because Steam refreshes
the licence list asynchronously after a grant and fifty repaired files would otherwise ask fifty times.
Telling the user this before they press the button is the restorer's job rather than the check's — preflight
should not have to know what Steam does to an account, and the same sentence is wanted on more than one
surface — and `SteamToolIDs` is the data that answers it, since a game with none has no companion app to add.

A tool app that still cannot be opened does not end the restore — the game's own depots hold everything but
the tool's files — but it is what gets reported if nothing else answered, because "no manifest has that
file" would be a claim about a manifest nobody managed to read. The game's own refusal **outranks** a tool's:
an account that does not hold Skyrim Special Edition is told about app 489830, not sent after a free tool it
never needed, so the two are kept in separate variables rather than letting whichever arrived first win.

`IGameFileRestorer.Consequences(games)` is that sentence, and it is the only copy: separate from `Status`,
which asks whether a repair could run at all, because this depends on *which* files are in it. The Steam
side reads `SteamToolIDs` for the apps in reach and names them from `FreeLicenseApps`, a table keyed by app
id, so an app nobody has named still reads as the game's app rather than as nothing. It is per-game and
never per-file, which is a limit rather than an oversight — whether a tool app is actually reached is not
known until its depots are searched, which is after the licence would have been taken — so the wording
promises "if one of them turns out to be needed" and not that it will be. The card, the check's detail and
the CLI verb all say it, and only when a game in the repair has a tool app at all.

**The WPF side of it.** `App.xaml.cs` calls `AddSteam`, registering its own `SteamGuardPrompt` first
because `AddSteam`'s `TryAdd`ed default raises an intervention this app answers by throwing. The prompt is
a singleton publishing whatever Steam Guard is asking as a `Pending` request; the pane binds to it and
answers, and null out of the two code questions is the only way out of SteamKit's infinite retry loop.
`SteamLoginVM` runs one attempt at a time, since `SteamSession` holds its login lock across a whole flow
and refuses a second caller — switching between the QR and the password paths cancels the attempt in
flight and waits for it to unwind. The challenge URL is drawn as a real code by `QrCodeView` (rectangles,
a whole device pixel per module, aliased edges, black on white whatever the theme), and Steam rotates it
from its polling thread, so it is marshalled. The pane is a floating one — `ShowSteamLogin` — used both by
preflight and by the Logins settings tile, which exists mainly so a saved login has a visible way out:
`SteamLoginManager` is an `INeedsLogin` whose `LoginFor` deliberately matches no downloader, because Steam
is not a download source and nothing needs it logged in ahead of time. `GameFilesVM` owns the game-files
card and the whole sequence behind `repair-game-files`: log in if there is no login, fetch with per-file
progress, then re-run the check, which is what decides whether the run carries on.

Preflight is the only thing that downloads. `AInstaller` has no download path of its own: `Begin` hashes
the downloads folder once and returns `DownloadFailed` if anything the list still needs is absent, so every
install has to go through preflight first. Automated sources are WabbajackCDN, Http and premium Nexus; every other state
becomes a manual download whose browser URL comes from `ManualDownloadUrls.TryGet`. Partition by **state
type**, never by which downloader the dispatcher would choose.

**Manual downloads run before automated ones** (600 then 700, disk space still last): the user does the part
that needs their hands first, then walks away while the rest is fetched. manual-downloads depends on
archive-inventory, unsupported-archives and nexus-login; automated-downloads depends on manual-downloads.
The split both work from is `Rules/DownloadPlan` — the allow-list and mirror load, the mirror reroute, the
Nexus premium probe, the automated/manual partition and the screening of what came out automated — computed
on demand by whichever of them asks first and memoised on the blackboard, so the probe and the policy load
happen once per run. Computing it also fills the manual queue with everything it means to send to the
browser. Writing `RequiredArchives` throws the memo away, so re-running archive-inventory repartitions
against the new answer, and so does a change to the Nexus premium answer — a free account that logs in again
as premium would otherwise keep the split made while it was free, and be told to fetch by hand what the app
can now download. The manual queue goes with the plan, since the queue is what that plan sent to the browser.
Re-probing the same account changes nothing and costs nothing.

The reroute rewrites the modlist's own `Archive.State`, so after a plan has been computed the list looks as
though it always carried the mirror's states. `PreflightBlackboard.Rerouted` records what was rewritten, and
nexus-login ignores those: otherwise a list with no Nexus files of its own passes "this list has no Nexus
Mods files" on the first pass and halts for a login on the second, contradicting the rule the reroute path
is built on — a rerouted Nexus download without a login goes to the manual queue rather than stopping the
run.

Splitting by state type is only half of it. An archive whose state is automated still needs a downloader the
dispatcher has, one that will `Prepare()`, and a URL the allow-list permits, and none of those takes a
download to establish, so `ArchiveDownloadPipeline.Screen` settles them while the plan is being computed.
Screened out at plan time, those archives reach the manual queue before manual-downloads reads it; screened
out later, they would arrive after it had already passed and reported nothing to do by hand.
`ManualDownloadsCheck` reads the queue only after asking for the plan for the same reason — the only
shortcut past it is nothing missing at all.

**One definition of being logged in to Nexus Mods, and it is the downloader's.** `NexusCredential.CanDownload`
in `Wabbajack.Networking.NexusApi` is it, over the `NexusCredentialSource` that `NexusApi.CredentialSource()`
resolves from the same code that builds the request headers. `NexusDownloader.Prepare` and
`NexusApiLoginProbe` both ask it and nothing else. They used to decide separately, and disagreed over
`NEXUS_API_KEY`: `NexusApi` falls back to that variable for its own calls, so the probe validated with it and
reported a premium login, while `Prepare` — which reads the stored OAuth state before every download, and
whose `ITokenProvider.Get` throws when nothing is stored — returned false and sent every Nexus archive to the
browser. A green "logged in" row and a pile of manual links. The variable stays the CLI's and the test
suite's way into the raw API; it is simply not a login. Whatever `NexusLoginCheck` reports has to be
something the download path can deliver, and the row says *how* the user is authenticated: a stored API key
reads "Logged in as X (API key)", and `NEXUS_API_KEY` with nothing stored reads as logged out with a detail
naming the variable, because the variable working everywhere else is exactly what makes it confusing. The
WPF Nexus tile (`NexusLoginManager`) asks the same predicate rather than testing the stored token itself,
which is what kept a stored API key reading "logged out" there and "Logged in (API key)" in preflight.
The predicate is true for a credential Nexus has since revoked, so `TriggerLogin` carries no `canExecute`:
"your login has expired, log in again" is a row whose `LoggedIn` is true, and a `ReactiveCommand` that
refuses puts the refusal in `ThrownExceptions`, which nothing here observes and which surfaces on the UI
thread. Nothing executes a sibling command either — `ToggleLogin` calls the work directly. That button is
the tile's only one, so it falls through to the login when logging out cannot change anything: `LoggedIn`
means a usable credential is in reach, not that there is a file to delete, and a host that supplies
`NEXUS_OAUTH_INFO` would otherwise get a button reading "Log out" for ever. A login stored by the browser
shadows the variable, so that fall-through is a real way out. One login window at a time, too —
`MainWindowVM` serialises browser windows, so a second request would open behind the first rather than
being dropped.

A source is only reported when there is something to send with it. `GetAuthInfo` rejects an empty API key
either side, and equally an OAuth state carrying no access token — which is not hypothetical: a refused
refresh deserializes the error body into a `JwtTokenReply` with a null `access_token`, and older versions
stored that over the login, so a user may already have one. Reported as `OAuth` it would pass `CanDownload`
and then throw out of `AddAuthHeaders` on the first real request. The stored API key in the same state is
still a login; the environment variable is not consulted, because a stored login shadows it whether or not
it turned out to be usable. `NexusCredentialTests` is the table.

**A refusal from the token endpoint must not write anything.** Both places that talk to it follow the same
rule. `RefreshToken` stores only a reply that actually carries an access token, and hands the caller a copy
with nothing to send for this call; `NexusLoginHandler.StateToStore` decides the same thing for the login
window, and keeps the stored `ApiKey`, which that exchange says nothing about. Storing the failure
destroyed a login whose refresh token the next attempt might have used — and since the login tile reads the
credential from its own constructor and its Log in button now works while logged in, both paths are
reachable with a working login to lose. Neither throws: a browser operation is driven from an `async void`
handler.

A refused refresh also stands for `RefreshRetryDelay` (a minute) before another is attempted. `GetAuthInfo`
refreshes an expired token on the way to every authenticated call, so an offline machine would otherwise
post a doomed refresh once per request; the old behaviour hid that by writing the failure and never trying
again at all.

`CanDownload` answers "can this machine download from Nexus Mods" and nothing else; the collection upload
and download paths build their own GraphQL requests with `Authorization: Bearer`, so they need an unexpired
OAuth access token specifically and read the stored state for one. A stored API key passes `CanDownload`
and would fail there.

An automated download can still turn out to need a browser once it is running — a
`ManualDownloadRequiredException`, a stall, a server refusal, bytes that do not hash — which fills a queue
manual-downloads has already emptied. automated-downloads therefore ends `NeedsUser` whenever the queue is
not empty when it finishes, offering `PreflightAction.DownloadByHand`, which `PreflightActionDispatcher`
maps to re-running manual-downloads; that picks up the new items, and the run reaches `Ready` only once
nothing is left. A retry of automated-downloads leaves queued archives alone rather than downloading them
into the queue again.

`ManualDownloadAcquirer` watches a folder (the user's
Downloads by default, `KnownFolders.Downloads`) for files matching pending archives by size,
then hash. Its completion signal is an exclusive open (`FileShare.None`), not size — pre-allocating
downloaders report the final size from the first byte. Cross-volume placement copies through a
`.wj_incoming` temp name and renames, so cancellation never leaves a partial in the downloads folder.

Per-archive identity everywhere is `Archive.Name`, compared ordinal-ignore-case. Two archives can share a
hash under different names, so `Hash` is not a key.

`OptimizeModlist` and preflight share `Rules/RequiredArchives` for "which archives does this install still
need"; `RequiredArchivesTests.MatchesOptimizeModlistPruning` pins that they agree. The deletion phases in
`OptimizeModlist` are unchanged and stay under `FileDeletionRules`.

Timing-sensitive tests (the acquirer, the download checks) use fast `ManualDownloadAcquirerOptions` and
run several times in a row before they are considered green. `FileHashCache` keeps its SQLite connection
open and is not disposable, so test hosts keep its file outside the per-test temporary root.

## Testing

The offline integration harness is the most useful thing here. `Wabbajack.Compiler.Test/ModListHarness.cs`
builds a synthetic modlist, compiles it and installs it; `Wabbajack.Installer.Test` installs the checked-in
`TestData/MO2AndSKSETest.wabbajack`. Extending these reaches real compile and install behaviour without a
game install or a large download.

Reach for a pure function plus a table of cases where behaviour is fiddly and the consequences are
destructive; `FileDeletionRulesTests` is the model. Concurrency bugs need a sample large enough to fail
every run rather than one run in three, so prefer tens of thousands of iterations over a few hundred.

The WPF project has close to no test coverage and is not a good place to add it.
