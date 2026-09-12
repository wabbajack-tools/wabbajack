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
uppercased with dashes turned into underscores, so `lovers-lab` reads `LOVERS_LAB`. Stored tokens live in
`%LOCALAPPDATA%\Wabbajack\encrypted\`.

## Layout

Projects are small and single-purpose. The name says what is inside.

| Area | Projects |
|---|---|
| Paths and IO | `Wabbajack.Paths`, `Wabbajack.Paths.IO`, `Wabbajack.IO.Async` |
| Core flows | `Wabbajack.Compiler`, `Wabbajack.Installer`, `Wabbajack.VFS` |
| Archives | `Wabbajack.Compression.BSA`, `Wabbajack.Compression.Zip`, `Wabbajack.FileExtractor` |
| Downloads | `Wabbajack.Downloaders.*`, dispatched by `Wabbajack.Downloaders.Dispatcher` |
| Network clients | `Wabbajack.Networking.*` |
| Serialization | `Wabbajack.DTOs` plus the `Wabbajack.DTOs.ConverterGenerators` source generator |
| Wiring | `Wabbajack.Services.OSIntegrated` registers nearly everything in DI |
| UI | `Wabbajack.App.Wpf` (ReactiveUI), `Wabbajack.Launcher` |

Packages are pinned centrally in `Directory.Packages.props`. A `<PackageReference>` carries no version.

**FluentAssertions stays on 7.2.2 permanently.** 8.0 left Apache 2.0 for a paid Xceed licence. Do not
upgrade it, and do not add it to a project that does not already use it.

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

## Testing

The offline integration harness is the most useful thing here. `Wabbajack.Compiler.Test/ModListHarness.cs`
builds a synthetic modlist, compiles it and installs it; `Wabbajack.Installer.Test` installs the checked-in
`TestData/MO2AndSKSETest.wabbajack`. Extending these reaches real compile and install behaviour without a
game install or a large download.

Reach for a pure function plus a table of cases where behaviour is fiddly and the consequences are
destructive; `FileDeletionRulesTests` is the model. Concurrency bugs need a sample large enough to fail
every run rather than one run in three, so prefer tens of thousands of iterations over a few hundred.

The WPF project has close to no test coverage and is not a good place to add it.
