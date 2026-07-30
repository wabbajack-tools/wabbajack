# Wabbajack: WPF → Avalonia Port Plan

Status: planning. Branch: `halgari/avalonia-everywhere`.

## Goal & constraints

- Move the desktop UI off **WPF** onto **Avalonia**. **Windows-only for now** — no cross-platform
  abstraction work on game locators, registry, paths, or interop.
- Target `net10.0-windows`, win-x64.
- Keep the existing ViewModel and business layers. This is a **View-layer** port; app logic is
  already UI-framework-agnostic (ReactiveUI + `[Reactive]` partial properties + `MessageBus`).
- Avalonia **11.3.x** — the same stack `Wabbajack.Launcher` already ships in this repo. We do **not**
  need Avalonia 12 (its native cross-platform WebView is unnecessary; see Browser).
- ReactiveUI: `ReactiveUI.WPF` → `Avalonia.ReactiveUI`; keep `ReactiveUI.SourceGenerators`.

## Decisions

- **Structure:** parallel project. New Avalonia head is **`Wabbajack.App.Avalonia`**; shared code moves
  to **`Wabbajack.App.Core`**. The WPF project keeps building/running until cutover, and views migrate
  incrementally. (Rejected: in-place conversion — leaves the app unbuildable for the whole port.)
- **Theme:** faithful re-creation. Port the brush palette and re-author `Themes/Styles.xaml`
  (~1,462 style/template/resource constructs) + `CustomControls.xaml` so the app looks the same.
- **Browser:** `WebView2.Avalonia` (BeyondDimension, MIT), which exposes `CoreWebView2`, so the
  cookie/request-interception/JS logic ports nearly verbatim. Windows keeps using Edge WebView2.
- **CefSharp:** delete. It is dead code today (`CefService.CreateBrowser()` returns 0, VM lines
  commented out).

## Target project layout

```
Wabbajack.App.Core        VMs, services, messages, converters logic, navigation — no UI-framework refs
Wabbajack.App.Wpf         existing head; references Core; stays until cutover, then deleted
Wabbajack.App.Avalonia    new Avalonia head; references Core; Program.cs + App.axaml + ViewLocator
```

The VMs are almost UI-agnostic already. The few WPF couplings that must be abstracted into `Core`:

- **Browser:** `BrowserWindowViewModel` holds a `WebView2` field and drives it. Introduce an
  `IWabbajackWebView` abstraction (navigate, cookies, exec JS, resource-request headers, DOM) in Core;
  each head provides the concrete WebView2 host. Handler logic (Nexus/OAuth2/manual download) lives in Core.
- **Images:** `ImageCacheManager`/converters use `BitmapImage`/`BitmapFrame`. Abstract to a stream/bytes
  producer in Core; each head decodes to its own bitmap type (Avalonia `Bitmap`).
- **Taskbar/scheduler/dialogs:** `TaskbarItemInfo`, `DispatcherScheduler`, `MessageBox.Show`,
  `DependencyObject` custom-control bits stay in the head projects.

## Effort tiers

| Tier | Scope |
|---|---|
| Free / near-free | 44 VMs, ReactiveUI wiring (`WhenActivated`/`BindCommand`/`WhenAnyValue`), DI/Host bootstrap, message-bus navigation, 18 converters, business project refs |
| Medium | 51 views' markup → `.axaml`; `Styles.xaml` + `CustomControls.xaml` re-author; image handling; icon swap (`FluentIcons.Wpf` → `FluentIcons.Avalonia`) |
| Hard / risk | WebView2 subsystem; MahApps `MetroWindow` chrome + taskbar progress; WPF-only controls (`Sdl.MultiSelectComboBox`, `Extended.Wpf.Toolkit`, `MathConverter`) |

## Dependency remap

| WPF package | Fate |
|---|---|
| `ReactiveUI.WPF` | → `Avalonia.ReactiveUI` |
| `Microsoft.Web.WebView2` | → `WebView2.Avalonia` (exposes `CoreWebView2`) |
| CefSharp (`<Reference>`s) | delete (dead code) |
| `MahApps.Metro` | re-implement: Avalonia `Window` + `ExtendClientAreaToDecorationsHint` custom titlebar; `ProgressBar` |
| `FluentIcons.Wpf` | → `FluentIcons.Avalonia` |
| `WPFThemes.DarkBlend` | → Avalonia `FluentTheme` + ported brushes |
| `MathConverter`, `Sdl.MultiSelectComboBox`, `Extended.Wpf.Toolkit` | no Avalonia builds — replace per use site |
| `Microsoft-WindowsAPICodePack-Shell` | Avalonia `StorageProvider` or keep P/Invoke |
| `Orc.FileAssociation`, `PInvoke.User32`, `Silk.NET.DXGI` | keep (UI-agnostic Windows interop) |
| DynamicData, System.Reactive, ImageSharp, Humanizer, Extensions.\*, NLog, HtmlAgilityPack | unchanged |

## Browser subsystem

Active browser is WebView2 (`BrowserWindowViewModel`), used for Nexus OAuth, LoversLab/VectorPlexus
IPS4 OAuth2, and manual/gated downloads. Required capabilities:

- navigation + `NavigationCompleted`
- `ExecuteScriptAsync` (DOM scrape, iframe stripping)
- `CoreWebView2.CookieManager.GetCookiesAsync` (session capture — central to logins)
- `AddWebResourceRequestedFilter` + `WebResourceRequested` (inject `Application-Name`/`Version` headers)
- `GoBack`

`WebView2.Avalonia` surfaces `CoreWebView2`, so this should port with minimal change — but it is the
single biggest risk, so it is proven **first** (Phase 0) before any view work.

## Phases

- **Phase 0 — WebView2 spike (critical, do first).** Throwaway Avalonia window hosting
  `WebView2.Avalonia`. Verify `CoreWebView2` exposes CookieManager + WebResourceRequested +
  ExecuteScriptAsync, then run one real Nexus login end-to-end. Go/no-go gate for the chosen browser lib.
- **Phase 1 — Core extraction + scaffold.** Create `Wabbajack.App.Core`; move VMs/services/messages;
  introduce `IWabbajackWebView` and image abstractions; point `Wabbajack.App.Wpf` at Core (keeps it
  green). Create `Wabbajack.App.Avalonia` from the Launcher bootstrap (`Program.cs`/`App.axaml`/
  `ViewLocator`, `Host.CreateDefaultBuilder`, `UseReactiveUI`). Empty `MainWindow` + navigation shell
  running against the real VMs.
- **Phase 2 — Theme foundation.** Port color/brush resources and base control styles onto `FluentTheme`;
  establish the design tokens the views bind to. This is the largest single chunk.
- **Phase 3 — Views (dependency order).** `Views/Common` reusable controls → shell
  (`MainWindow`/`NavigationView`) → screens (Home, Gallery, Details, Installer, Compiler, Settings) →
  interventions. Replace `DataTemplate DataType` maps with `ViewLocator`/`DataTemplates`.
- **Phase 4 — Browser integration.** Wire the Phase 0 spike into the real login/download handlers via
  `IWabbajackWebView`.
- **Phase 5 — Shell polish.** Custom titlebar (`ExtendClientAreaToDecorationsHint`), taskbar progress
  (P/Invoke `ITaskbarList3` — no Avalonia built-in), single-instance, `wabbajack://` protocol + file assoc.
- **Phase 6 — Cutover.** Switch the launched/published project to `Wabbajack.App.Avalonia`; delete
  `Wabbajack.App.Wpf` + WPF-only deps; update CI/release packaging; port the `InvalidProgramException`
  VM regression test to the new project.

## Open items / notes

- Shared-core project name `Wabbajack.App.Core` is a proposal — rename if preferred.
- Taskbar progress and exact window chrome fidelity: replicate via P/Invoke, or simplify. TBD in Phase 5.
- `MathConverter` inline-math bindings: enumerate use sites during Phase 3 and replace with converters or
  precomputed VM properties.
