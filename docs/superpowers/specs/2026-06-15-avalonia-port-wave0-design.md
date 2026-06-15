# Avalonia Port — Wave 0 (Foundation + HomeView slice + headless tests)

## Context

Wabbajack's desktop UI is a mature WPF app (`Wabbajack.App.Wpf`, `net10.0-windows`) that
only runs on Windows and cannot be UI-tested headlessly. We are porting it to **Avalonia** so
the app runs cross-platform and, more immediately, so we can write **headless UI tests**. That
testing capability is the primary driver for doing this now, ahead of Linux filepath work.

A prior refactor already extracted a platform-neutral `Wabbajack.App.Core` (`net10.0`) holding
the `ViewModel` base, several ViewModels (`HomeVM`, `FilePickerVM`, `PreflightViewModel`), the
reactive extensions, and three UI abstractions — `IFileSelector`, `IDialogService`,
`IImageService` — with WPF implementations in the WPF project. That work is what makes an
Avalonia port tractable: ViewModels are (or are becoming) toolkit-agnostic.

**Decisions taken (brainstorming):**
- **Full replacement, high fidelity.** Avalonia will replace WPF entirely; the new UI should
  closely re-create the current look (brand colors, custom control styling, layouts), not just
  re-theme onto stock Fluent.
- **No embedded web browser.** CefSharp is not active; `WebBrowserVM`/`CefService`/
  `WebBrowserView` are out of scope. "Open a page" goes to the system browser.
- **Parallel, wave-based execution.** Build a new Avalonia app beside the WPF one; port in
  waves; WPF stays shippable until parity; delete WPF in the final wave.

This spec covers **Wave 0** only — the foundation plus one full vertical slice — which proves
every reusable pattern. Later waves each get their own spec.

### Hard prerequisite that shapes everything
An Avalonia app (`net10.0`) cannot reference the WPF assembly (`net10.0-windows`). So
"port to Avalonia" means **every ViewModel must live in `Wabbajack.App.Core`**. Wave 0 only
needs the already-moved `HomeVM`; subsequent waves move VM groups (compiler, installer,
settings, gallery, login) to Core as they are ported.

### Overall wave plan (context; only Wave 0 is specified here)
- **Wave 0 (this spec):** Avalonia project + bootstrap/DI + theme foundation + `HomeView`
  slice (`BigButton`, `LinksView`, brand brushes, FluentIcons) + `Avalonia.Headless` TUnit
  harness with the first UI test.
- **Waves 1..N:** move each remaining VM group to Core, port its views + custom controls,
  add headless tests.
- **Final wave:** reach parity, delete `Wabbajack.App.Wpf`, repoint build/release/launcher.

## Wave 0 design

### 1. New project & structure
Create `Wabbajack.App.Avalonia` (Avalonia 11.3.14, `net10.0`, cross-platform), copying the
proven package set from the existing `Wabbajack.Launcher` Avalonia project: `Avalonia`,
`Avalonia.Desktop`, `Avalonia.ReactiveUI`, `Avalonia.Themes.Fluent`, `Avalonia.Diagnostics`,
`MessageBox.Avalonia`, `ReactiveUI.SourceGenerators`, `Microsoft.Extensions.*` — plus
`FluentIcons.Avalonia`. References: `Wabbajack.App.Core`, `Wabbajack.Lib`, `Wabbajack.CLI`,
`Wabbajack.CLI.Builder` (mirroring the WPF project). It builds **alongside** the WPF app with a
distinct `AssemblyName` during coexistence (WPF's is `Wabbajack`; the Avalonia app uses a
temporary distinct name until WPF is retired). Folder layout mirrors WPF: `Views/`,
`Controls/`, `Styles/`, `App.axaml`, `Program.cs`.

### 2. App bootstrap, DI & navigation
- `Program.cs`: standard Avalonia `AppBuilder` → classic desktop lifetime.
- `App.axaml` / `App.axaml.cs`: include `FluentTheme` + the ported brand `Styles`; build the DI
  container.
- **DI** via `Microsoft.Extensions.DependencyInjection`, reusing `AddViewModels()` and
  `AddOSIntegrated()`, and registering **Avalonia implementations** of the three abstractions:
  `AvaloniaFileSelector` (Avalonia `StorageProvider` pickers), `AvaloniaDialogService`
  (`MessageBox.Avalonia`), `AvaloniaImageService` (decode to Avalonia `Bitmap`). The opaque
  `object` image handle the VMs already use binds directly to an Avalonia `Image.Source`.
- **VM→View resolution:** a ReactiveUI `IViewLocator` (or `DataTemplate`s) mapping a ViewModel
  to its `ReactiveUserControl`.
- **Navigation:** reuse the existing `NavigateToGlobal` / `ScreenType` message bus already in
  Core — no new navigation system. A minimal `MainWindow` shell hosts a content presenter that
  swaps the active view on navigation; Wave 0 only needs it to display `HomeView`.

### 3. Theming & the HomeView vertical slice (high fidelity)
- **Theme foundation:** port the brand resource palette from the WPF `ResourceDictionary`s into
  an Avalonia `ResourceDictionary`/`Styles` — at minimum the brushes `HomeView` uses
  (`ForegroundBrush`, `PrimaryBrush`, `ComplementaryPrimary08Brush`, `DarkSecondaryBrush`) and
  their source colors, plus fonts as needed. This establishes the brand-resource porting
  pattern reused by every later view.
- **`BigButton` control:** port the WPF custom control (`DependencyProperty` + `UserControlRx`)
  to an Avalonia `ReactiveUserControl` using `StyledProperty` for `Title`, `Description`,
  `Icon`, `ButtonStyle`, `Command`; style it to match the current look. This is the canonical
  custom-control porting example.
- **`LinksView` sub-view:** port the small links row so the slice exercises a nested view; if it
  pulls in further controls (e.g. icon-link buttons) those are ported with it.
- **`HomeView.axaml`:** recreate the layout bound to `HomeVM` (`[Reactive] Modlists`,
  `BrowseCommand`, `GetHelpCommand`, `VisitModlistWizardCommand`), using `FluentIcons.Avalonia`
  `SymbolIcon`s. Result: visually ~identical to WPF and fully functional.

### 4. Headless test harness
Add `Avalonia.Headless` UI testing into the existing **TUnit** test setup. Prefer an
`Avalonia.Headless.TUnit` integration if one exists; otherwise add a small bridge — a headless
`AppBuilder` initialized once for the assembly (with `UseHeadless` + `UseReactiveUI`) and test
bodies dispatched onto the Avalonia UI thread. Whether these tests live in the existing
`Wabbajack.Test` assembly or a new `Wabbajack.App.Avalonia.Test` assembly is an implementation
detail to settle in the plan (a separate assembly avoids coupling the headless Avalonia
platform to the rest of the suite). First headless UI test(s): construct `HomeView` against a
real DI-resolved `HomeVM`, render it headlessly, assert it loads, then invoke `BrowseCommand`
and assert a `NavigateToGlobal(ScreenType.ModListGallery)` message fires — a genuine
rendered-UI test with no display.

### 5. Out of scope for Wave 0
All other screens/VMs; moving non-`HomeVM` ViewModels to Core; the web browser; deleting the
WPF project; release/packaging changes. These are later waves.

## Verification

Wave 0 is complete when all hold:
1. **Builds cross-platform:** `dotnet build` of `Wabbajack.App.Avalonia` succeeds on Windows and
   on Linux/WSL (`net10.0`), and the full solution still builds.
2. **Runs:** launching the Avalonia app shows a high-fidelity `HomeView` whose buttons/links work
   (e.g. "Get Started" / "navigate the gallery" trigger navigation).
3. **Headless UI test passes:** the new `Avalonia.Headless` TUnit test(s) render `HomeView` and
   verify `BrowseCommand` → `NavigateToGlobal` with no display, green locally and in CI.
4. **No regressions:** the existing 185-passing test suite remains green; the WPF app still
   builds and runs.

## Risks / open items (to resolve during planning)
- **`Avalonia.Headless` + TUnit integration:** confirm a package exists or build the bridge.
  Note: the headless Avalonia platform is process-global, so its tests may need to be isolated
  (separate assembly, or `[NotInParallel]`) from the existing suite.
- **FluentIcons symbol parity:** confirm `FluentIcons.Avalonia` exposes the symbols `HomeView`
  uses (`Search`, `ArrowDownload`, `Games`, `ChevronRight`, etc.).
- **Coexistence assembly naming:** WPF's `AssemblyName` is `Wabbajack`; pick a non-conflicting
  name for the Avalonia app until WPF is removed.
