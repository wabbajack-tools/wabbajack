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

`Wabbajack.Installer/Preflight` runs a checklist before an install starts: Nexus login and premium status,
game installed, game files, archive inventory, unsupported archives, manual downloads, automated downloads,
disk space. `PreflightRunner.Create(services, config)` mirrors `StandardInstaller.Create`; checks come from
DI as `IPreflightCheck` and run in `Order` (100–900, gaps left on purpose), each declaring `DependsOn`. A
check that fails or needs the user makes its dependents `Skipped`; `RunCheck(id)` re-runs one and resets
what depends on it. The engine has no UI and no DynamicData anywhere. The runner is event-based: it raises
plain events (`CheckChanged`, `ArchiveChanged`, `ManualQueueChanged`, `RunFinished`) and offers snapshots;
hosts project those however they like. The acquirer exposes `IObservable`s via `System.Reactive` (already
a transitive dependency).

Preflight is the only thing that downloads. `AInstaller` has no download path of its own: `Begin` hashes
the downloads folder once and returns `DownloadFailed` if anything the list still needs is absent, so every
install has to go through preflight first. Automated sources are WabbajackCDN, Http and premium Nexus; every other state
becomes a manual download whose browser URL comes from `ManualDownloadUrls.TryGet`. Partition by **state
type**, never by which downloader the dispatcher would choose.

**Manual downloads run before automated ones** (600 then 700, disk space still last): the user does the part
that needs their hands first, then walks away while the rest is fetched. manual-downloads depends on
archive-inventory, nexus-login and unsupported-archives; automated-downloads depends on manual-downloads.
The split both work from is `Rules/DownloadPlan` — the allow-list and mirror load, the mirror reroute, the
Nexus premium probe, the automated/manual partition and the screening of what came out automated — computed
on demand by whichever of them asks first and memoised on the blackboard, so the probe and the policy load
happen once per run. Computing it also fills the manual queue with everything it means to send to the
browser. Writing `RequiredArchives` throws the memo away, so re-running archive-inventory repartitions
against the new answer.

Splitting by state type is only half of it. An archive whose state is automated still needs a downloader the
dispatcher has, one that will `Prepare()`, and a URL the allow-list permits, and none of those takes a
download to establish, so `ArchiveDownloadPipeline.Screen` settles them while the plan is being computed.
That matters because the two can disagree: `NexusApiLoginProbe` counts `NEXUS_API_KEY` as a login while
`NexusDownloader.Prepare` only looks at the stored token, so an account the probe calls premium can still
have nothing to download with. Screened out at plan time, those archives reach the manual queue before
manual-downloads reads it; screened out later, they would arrive after it had already passed and reported
nothing to do by hand. `ManualDownloadsCheck` reads the queue only after asking for the plan for the same
reason — the only shortcut past it is nothing missing at all.

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
