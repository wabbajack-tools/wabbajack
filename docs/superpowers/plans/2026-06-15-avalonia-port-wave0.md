# Avalonia Port — Wave 0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a new cross-platform Avalonia app (`Wabbajack.App.Avalonia`) that reuses the existing `Wabbajack.App.Core` ViewModels, renders a high-fidelity `HomeView`, and is covered by a real headless UI test — proving every pattern needed to port the rest of the app.

**Architecture:** A parallel Avalonia 11.3.14 app beside the WPF app (deleted only in the final wave). DI via `Microsoft.Extensions.DependencyInjection`; a ReactiveUI-style `ViewLocator` maps VM→View; navigation reuses the existing `NavigateToGlobal` message bus already in Core; the three UI abstractions (`IFileSelector`/`IDialogService`/`IImageService`) get Avalonia implementations. Headless tests use Avalonia's framework-agnostic `HeadlessUnitTestSession` driven from TUnit.

**Tech Stack:** Avalonia 11.3.14, `Avalonia.ReactiveUI`, `Avalonia.Themes.Fluent`, `Avalonia.Headless`, `FluentIcons.Avalonia`, `MessageBox.Avalonia`, `Microsoft.Extensions.DependencyInjection/Hosting`, TUnit, `Wabbajack.App.Core`, `Wabbajack.Lib`.

**Reference files (read these while implementing):**
- Bootstrap pattern: `Wabbajack.Launcher/{Program.cs,App.axaml,App.axaml.cs,ViewLocator.cs}`
- Source views to translate: `Wabbajack.App.Wpf/Views/HomeView.xaml`, `Views/Common/BigButton.xaml(.cs)`, `Views/LinksView.xaml(.cs)`
- Brand colors/brushes: `Wabbajack.App.Wpf/Themes/Styles.xaml`
- ViewModel under test: `Wabbajack.App.Core/ViewModels/HomeVM.cs`
- Navigation message: `Wabbajack.App.Core/Messages/NavigateToGlobal.cs`

**Convention note:** During coexistence the WPF app's `AssemblyName` is `Wabbajack`; this project uses `AssemblyName=WabbajackAvalonia` to avoid a clash. Avalonia XAML files use the `.axaml` extension and the `https://github.com/avaloniaui` namespace.

---

### Task 1: Avalonia project skeleton that builds and runs an empty window

**Files:**
- Create: `Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj`
- Create: `Wabbajack.App.Avalonia/Program.cs`
- Create: `Wabbajack.App.Avalonia/App.axaml`
- Create: `Wabbajack.App.Avalonia/App.axaml.cs`
- Create: `Wabbajack.App.Avalonia/ViewLocator.cs`
- Create: `Wabbajack.App.Avalonia/Views/MainWindow.axaml`
- Create: `Wabbajack.App.Avalonia/Views/MainWindow.axaml.cs`
- Modify: `Wabbajack.sln` (add project)

- [ ] **Step 1: Create the csproj**

`Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework Condition=" '$(OS)' == 'Windows_NT'">net10.0-windows</TargetFramework>
    <TargetFramework Condition=" '$(OS)' != 'Windows_NT'">net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AvaloniaUseCompiledBindingsByDefault>false</AvaloniaUseCompiledBindingsByDefault>
    <AssemblyName>WabbajackAvalonia</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <NoWarn>CS8600;CS8601;CS8618;CS8604;CS1998</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.14" />
    <PackageReference Include="Avalonia.Desktop" Version="11.3.14" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.14" />
    <PackageReference Include="Avalonia.Diagnostics" Version="11.3.14" />
    <PackageReference Include="Avalonia.ReactiveUI" Version="11.3.9" />
    <PackageReference Include="FluentIcons.Avalonia" Version="1.1.299" />
    <PackageReference Include="MessageBox.Avalonia" Version="3.3.1.1" />
    <PackageReference Include="ReactiveUI.SourceGenerators" Version="2.6.30" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.7" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.7" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wabbajack.App.Core\Wabbajack.App.Core.csproj" />
    <ProjectReference Include="..\Wabbajack.Lib\Wabbajack.Lib.csproj" />
  </ItemGroup>
</Project>
```
Also create `Wabbajack.App.Avalonia/app.manifest` (copy `Wabbajack.App.Wpf/app.manifest` if it exists; otherwise a standard Avalonia manifest from `dotnet new avalonia.app`). If no manifest exists in WPF, remove the `<ApplicationManifest>` line.

> **Note on `FluentIcons.Avalonia` version:** verify the latest 1.x on nuget.org and adjust; it must expose `FluentIcons.Avalonia.SymbolIcon` and the `FluentIcons.Common.Symbol` enum (same enum the WPF control uses).

- [ ] **Step 2: ViewLocator (VM→View by name)**

`Wabbajack.App.Avalonia/ViewLocator.cs` — same convention as the Launcher but resolves across the app + Core assemblies:
```csharp
using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;

namespace Wabbajack;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "null" };
        var name = data.GetType().FullName!.Replace("VM", "View").Replace("ViewModel", "View");
        var type = Type.GetType(name) ?? Type.GetType(name + ", WabbajackAvalonia");
        if (type != null) return (Control) Activator.CreateInstance(type)!;
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) => data is IReactiveObject;
}
```
> Wabbajack's ViewModels end in `VM` (e.g. `HomeVM`), so map `VM`→`View` (`HomeVM`→`HomeView`). Views live in the Avalonia assembly (`WabbajackAvalonia`); the second `Type.GetType` resolves them.

- [ ] **Step 3: App.axaml + code-behind**

`Wabbajack.App.Avalonia/App.axaml`:
```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:Wabbajack"
             x:Class="Wabbajack.App">
    <Application.DataTemplates>
        <local:ViewLocator />
    </Application.DataTemplates>
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

`Wabbajack.App.Avalonia/App.axaml.cs`:
```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Wabbajack;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new Views.MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 4: MainWindow shell with a placeholder**

`Wabbajack.App.Avalonia/Views/MainWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="Wabbajack.Views.MainWindow"
        Width="1280" Height="800" Title="Wabbajack">
    <ContentControl x:Name="ActivePane" />
</Window>
```
`Wabbajack.App.Avalonia/Views/MainWindow.axaml.cs`:
```csharp
using Avalonia.Controls;

namespace Wabbajack.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ActivePane.Content = new TextBlock { Text = "Wabbajack (Avalonia) — Wave 0" };
    }
}
```
> `InitializeComponent()` is generated by Avalonia's XAML compiler. `ActivePane` is the generated field for the named `ContentControl`.

- [ ] **Step 5: Program.cs (minimal host + AppBuilder)**

`Wabbajack.App.Avalonia/Program.cs` — adapt from `Wabbajack.Launcher/Program.cs`:
```csharp
using System;
using Avalonia;
using Avalonia.ReactiveUI;

namespace Wabbajack;

internal class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}
```
> DI wiring is added in Task 6 once there is a ViewModel to resolve. Wave-0 Task 1 just proves the app builds and shows a window.

- [ ] **Step 6: Add to solution**

Run: `dotnet sln Wabbajack.sln add Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj`
Expected: "Project ... added to the solution."

- [ ] **Step 7: Build**

Run: `dotnet build Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj -p:EnableWindowsTargeting=true -nologo`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add Wabbajack.App.Avalonia Wabbajack.sln
git commit -m "Avalonia Wave 0: project skeleton + bootstrap (empty window)"
```

---

### Task 2: Avalonia implementations of the three UI abstractions

**Files:**
- Create: `Wabbajack.App.Avalonia/Services/AvaloniaDialogService.cs`
- Create: `Wabbajack.App.Avalonia/Services/AvaloniaImageService.cs`
- Create: `Wabbajack.App.Avalonia/Services/AvaloniaFileSelector.cs`

> These implement the Core interfaces `IDialogService`, `IImageService`, `IFileSelector` (in `Wabbajack.App.Core/Services/`). The `IImageService` returns `object?`; for Avalonia that object is an `Avalonia.Media.Imaging.Bitmap`, which binds directly to `Image.Source`.

- [ ] **Step 1: AvaloniaDialogService**

`Wabbajack.App.Avalonia/Services/AvaloniaDialogService.cs`:
```csharp
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace Wabbajack;

public sealed class AvaloniaDialogService : IDialogService
{
    public void ShowError(string message, string title)
    {
        // Fire-and-forget; the MessageBox shows on the UI thread.
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Error);
        _ = box.ShowAsync();
    }
}
```
> Confirm the `MessageBox.Avalonia` 3.x API names (`MessageBoxManager.GetMessageBoxStandard`, namespaces `MsBox.Avalonia*`). Adjust to the installed version if needed.

- [ ] **Step 2: AvaloniaImageService**

`Wabbajack.App.Avalonia/Services/AvaloniaImageService.cs`:
```csharp
using System;
using System.IO;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using Wabbajack.Models;

namespace Wabbajack;

public sealed class AvaloniaImageService : IImageService
{
    public IObservable<object?> DownloadImage(IObservable<string?> urls, Action<Exception> onError, LoadingLock loadingLock)
        => urls.SelectMany(url => Observable.FromAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(url)) return (object?) null;
            using var ll = loadingLock.WithLoading();
            try
            {
                using var http = new System.Net.Http.HttpClient();
                await using var stream = await http.GetStreamAsync(url);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                return (object?) new Bitmap(ms);
            }
            catch (Exception ex) { onError(ex); return null; }
        }));

    public object? FromStream(Stream stream)
    {
        if (stream.CanSeek) stream.Position = 0;
        return new Bitmap(stream);
    }
}
```
> This is a first, simple implementation (no caching/resize). Parity with the WPF `ImageCacheManager`/ImageSharp pipeline is a later-wave refinement; Wave 0 only needs HomeView, which has no images.

- [ ] **Step 3: AvaloniaFileSelector**

`Wabbajack.App.Avalonia/Services/AvaloniaFileSelector.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack;

public sealed class AvaloniaFileSelector : IFileSelector
{
    public AbsolutePath? SelectPath(FileSelectorRequest request)
    {
        var top = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var provider = top?.StorageProvider;
        if (provider is null) return null;

        IStorageFolder? start = null;
        if (request.InitialDirectory != default)
            start = provider.TryGetFolderFromPathAsync(request.InitialDirectory.ToString()).GetAwaiter().GetResult();

        if (request.IsFolderPicker)
        {
            var folders = provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = request.Title, AllowMultiple = false, SuggestedStartLocation = start
            }).GetAwaiter().GetResult();
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            return path is null ? null : (AbsolutePath) path;
        }

        var filters = request.Filters.Select(f => new FilePickerFileType(f.Description)
        {
            Patterns = f.Patterns.ToList()
        }).ToList();
        var files = provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = request.Title, AllowMultiple = false, SuggestedStartLocation = start,
            FileTypeFilter = filters.Count > 0 ? filters : null
        }).GetAwaiter().GetResult();
        var file = files.FirstOrDefault()?.TryGetLocalPath();
        return file is null ? null : (AbsolutePath) file;
    }
}
```
> `FileFilter`/`FileSelectorRequest` are defined in `Wabbajack.App.Core/Services/IFileSelector.cs`. `FilePickerVM` is not exercised in Wave 0, so this just needs to compile and register.

- [ ] **Step 4: Build**

Run: `dotnet build Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj -p:EnableWindowsTargeting=true -nologo`
Expected: `Build succeeded. 0 Error(s)`. (Fix any API-name drift in `MessageBox.Avalonia`/`FluentIcons.Avalonia` flagged here.)

- [ ] **Step 5: Commit**

```bash
git add Wabbajack.App.Avalonia/Services
git commit -m "Avalonia Wave 0: Avalonia implementations of IFileSelector/IDialogService/IImageService"
```

---

### Task 3: Port the brand theme resources

**Files:**
- Create: `Wabbajack.App.Avalonia/Styles/Brand.axaml`
- Modify: `Wabbajack.App.Avalonia/App.axaml` (merge the brand resources)

- [ ] **Step 1: Create the brand resource dictionary**

`Wabbajack.App.Avalonia/Styles/Brand.axaml` — port the colors/brushes `HomeView` uses (values from `Themes/Styles.xaml`). In Avalonia, colors/brushes go in a `ResourceDictionary` (not `Styles`):
```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Brand colors (from Wabbajack.App.Wpf/Themes/Styles.xaml) -->
    <Color x:Key="ForegroundColor">#E5E5E8</Color>
    <Color x:Key="PrimaryColor">#D8BAF8</Color>
    <Color x:Key="ComplementaryPrimary08Color">#383750</Color>
    <Color x:Key="DarkSecondaryColor">#363952</Color>
    <Color x:Key="WindowBackgroundColor">#222531</Color>

    <SolidColorBrush x:Key="ForegroundBrush" Color="{StaticResource ForegroundColor}" />
    <SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource PrimaryColor}" />
    <SolidColorBrush x:Key="ComplementaryPrimary08Brush" Color="{StaticResource ComplementaryPrimary08Color}" />
    <SolidColorBrush x:Key="DarkSecondaryBrush" Color="{StaticResource DarkSecondaryColor}" />
    <SolidColorBrush x:Key="WindowBackgroundBrush" Color="{StaticResource WindowBackgroundColor}" />
</ResourceDictionary>
```
> As later waves port more views, add their brushes here from `Themes/Styles.xaml`. Keep the resource keys identical to WPF so view markup ports 1:1.

- [ ] **Step 2: Merge into App.axaml**

Add to `App.axaml` after `</Application.Styles>`:
```xml
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceInclude Source="avares://WabbajackAvalonia/Styles/Brand.axaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
```
Set the window background in `MainWindow.axaml`: add `Background="{DynamicResource WindowBackgroundBrush}"` to the `<Window>`.

- [ ] **Step 3: Build**

Run: `dotnet build Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj -p:EnableWindowsTargeting=true -nologo`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add Wabbajack.App.Avalonia/Styles Wabbajack.App.Avalonia/App.axaml Wabbajack.App.Avalonia/Views/MainWindow.axaml
git commit -m "Avalonia Wave 0: brand theme resources (brushes/colors)"
```

---

### Task 4: Port the BigButton custom control

**Files:**
- Create: `Wabbajack.App.Avalonia/Controls/BigButton.axaml`
- Create: `Wabbajack.App.Avalonia/Controls/BigButton.axaml.cs`

> WPF source: `Views/Common/BigButton.xaml(.cs)`. Translation: `DependencyProperty` → Avalonia `StyledProperty`; bind directly in AXAML instead of code-behind `BindToStrict`. `ButtonStyle` enum lives in the WPF project — recreate it in the Avalonia project (`enum ButtonStyle { Mono, Color, Danger }`).

- [ ] **Step 1: Control code-behind with StyledProperties**

`Wabbajack.App.Avalonia/Controls/BigButton.axaml.cs`:
```csharp
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentIcons.Common;

namespace Wabbajack;

public enum ButtonStyle { Mono, Color, Danger }

public partial class BigButton : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<BigButton, string?>(nameof(Title));
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<BigButton, string?>(nameof(Description));
    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<BigButton, Symbol>(nameof(Icon));
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<BigButton, ICommand?>(nameof(Command));
    public static readonly StyledProperty<ButtonStyle> ButtonStyleProperty =
        AvaloniaProperty.Register<BigButton, ButtonStyle>(nameof(ButtonStyle), ButtonStyle.Mono);

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public Symbol Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public ButtonStyle ButtonStyle { get => GetValue(ButtonStyleProperty); set => SetValue(ButtonStyleProperty, value); }

    public BigButton() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 2: Control markup**

`Wabbajack.App.Avalonia/Controls/BigButton.axaml` (translated from WPF `BigButton.xaml`; binds to the control via `$parent[BigButton]`):
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ic="using:FluentIcons.Avalonia"
             x:Class="Wabbajack.BigButton"
             ClipToBounds="True">
    <Button x:Name="Button" HorizontalAlignment="Stretch" HorizontalContentAlignment="Stretch"
            Command="{Binding Command, RelativeSource={RelativeSource AncestorType=UserControl}}"
            Background="{DynamicResource ComplementaryPrimary08Brush}"
            CornerRadius="16" BorderThickness="0">
        <Grid Margin="16" ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto">
            <TextBlock Grid.Column="0" Grid.Row="0" VerticalAlignment="Center" FontWeight="DemiBold" FontSize="24" Margin="0,0,0,4"
                       Foreground="{DynamicResource ForegroundBrush}"
                       Text="{Binding Title, RelativeSource={RelativeSource AncestorType=UserControl}}" />
            <ic:SymbolIcon Grid.Row="0" Grid.Column="1" VerticalAlignment="Center" IconSize="28"
                           Foreground="{DynamicResource PrimaryBrush}"
                           Symbol="{Binding Icon, RelativeSource={RelativeSource AncestorType=UserControl}}" />
            <TextBlock Grid.Column="0" Grid.Row="1" Grid.ColumnSpan="2" FontSize="13" VerticalAlignment="Center"
                       TextWrapping="Wrap" Foreground="{DynamicResource ForegroundBrush}"
                       Text="{Binding Description, RelativeSource={RelativeSource AncestorType=UserControl}}" />
        </Grid>
    </Button>
</UserControl>
```
> `ButtonStyle` (Mono/Color/Danger) controlled distinct backgrounds in WPF. For Wave 0, the `Color` style is what HomeView uses; the background above approximates it. A faithful style selector (Avalonia `Styles` keyed on a `Classes` value driven by `ButtonStyle`) is a small follow-up; not blocking Wave 0. Confirm `FluentIcons.Avalonia.SymbolIcon`'s property names (`Symbol`, `IconSize`) against the installed package and adjust.

- [ ] **Step 3: Build**

Run: `dotnet build Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj -p:EnableWindowsTargeting=true -nologo`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add Wabbajack.App.Avalonia/Controls
git commit -m "Avalonia Wave 0: port BigButton custom control"
```

---

### Task 5: Port LinksView

**Files:**
- Create: `Wabbajack.App.Avalonia/Views/LinksView.axaml`
- Create: `Wabbajack.App.Avalonia/Views/LinksView.axaml.cs`

> WPF source: `Views/LinksView.xaml(.cs)` — four buttons (Patreon/GitHub/Discord/Wiki) with `Click` handlers that open URLs. Port the same four buttons; handlers open the system browser. URLs come from `Consts` (in Core): `WabbajackPatreonUri`, `WabbajackGithubUri`, `WabbajackDiscordUri`, `WabbajackWikiUri`.

- [ ] **Step 1: Markup**

`Wabbajack.App.Avalonia/Views/LinksView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ic="using:FluentIcons.Avalonia"
             x:Class="Wabbajack.LinksView">
    <StackPanel Spacing="16">
        <Button x:Name="PatreonButton" Click="Patreon_Click" Width="180" Height="49">
            <DockPanel LastChildFill="True">
                <ic:SymbolIcon DockPanel.Dock="Right" IconSize="24" Symbol="Heart" VerticalAlignment="Center"
                               Foreground="{DynamicResource PrimaryBrush}" Margin="0,0,16,0" />
                <TextBlock FontWeight="DemiBold" FontSize="18" VerticalAlignment="Center" Margin="16,0,0,0" Text="Patreon" />
            </DockPanel>
        </Button>
        <Button x:Name="GitHubButton" Click="GitHub_Click" Width="180" Height="49">
            <DockPanel LastChildFill="True">
                <ic:SymbolIcon DockPanel.Dock="Right" IconSize="24" Symbol="BranchFork" VerticalAlignment="Center"
                               Foreground="{DynamicResource PrimaryBrush}" Margin="0,0,16,0" />
                <TextBlock FontWeight="DemiBold" FontSize="18" VerticalAlignment="Center" Margin="16,0,0,0" Text="GitHub" />
            </DockPanel>
        </Button>
        <Button x:Name="DiscordButton" Click="Discord_Click" Width="180" Height="49">
            <DockPanel LastChildFill="True">
                <ic:SymbolIcon DockPanel.Dock="Right" IconSize="24" Symbol="PeopleChat" VerticalAlignment="Center"
                               Foreground="{DynamicResource PrimaryBrush}" Margin="0,0,16,0" />
                <TextBlock FontWeight="DemiBold" FontSize="18" VerticalAlignment="Center" Margin="16,0,0,0" Text="Discord" />
            </DockPanel>
        </Button>
        <Button x:Name="WikiButton" Click="Wiki_Click" Width="180" Height="49">
            <DockPanel LastChildFill="True">
                <ic:SymbolIcon DockPanel.Dock="Right" IconSize="24" Symbol="QuestionCircle" VerticalAlignment="Center"
                               Foreground="{DynamicResource PrimaryBrush}" Margin="0,0,16,0" />
                <TextBlock FontWeight="DemiBold" FontSize="18" VerticalAlignment="Center" Margin="16,0,0,0" Text="Wiki" />
            </DockPanel>
        </Button>
    </StackPanel>
</UserControl>
```

- [ ] **Step 2: Code-behind (open system browser)**

`Wabbajack.App.Avalonia/Views/LinksView.axaml.cs`:
```csharp
using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Wabbajack;

public partial class LinksView : UserControl
{
    public LinksView() => AvaloniaXamlLoader.Load(this);

    private static void Open(Uri url) =>
        Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });

    private void Patreon_Click(object? s, RoutedEventArgs e) => Open(Consts.WabbajackPatreonUri);
    private void GitHub_Click(object? s, RoutedEventArgs e) => Open(Consts.WabbajackGithubUri);
    private void Discord_Click(object? s, RoutedEventArgs e) => Open(Consts.WabbajackDiscordUri);
    private void Wiki_Click(object? s, RoutedEventArgs e) => Open(Consts.WabbajackWikiUri);
}
```
> `Consts` is in `Wabbajack.App.Core` (namespace `Wabbajack`). Confirm the four `Uri` members exist there (they do: `WabbajackPatreonUri`, `WabbajackGithubUri`, `WabbajackDiscordUri`, `WabbajackWikiUri`).

- [ ] **Step 3: Build + Commit**

Run: `dotnet build Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj -p:EnableWindowsTargeting=true -nologo` → `0 Error(s)`.
```bash
git add Wabbajack.App.Avalonia/Views/LinksView.axaml Wabbajack.App.Avalonia/Views/LinksView.axaml.cs
git commit -m "Avalonia Wave 0: port LinksView"
```

---

### Task 6: Port HomeView, wire DI + navigation, show it on startup

**Files:**
- Create: `Wabbajack.App.Avalonia/Views/HomeView.axaml`
- Create: `Wabbajack.App.Avalonia/Views/HomeView.axaml.cs`
- Modify: `Wabbajack.App.Avalonia/Program.cs` (DI host)
- Modify: `Wabbajack.App.Avalonia/App.axaml.cs` (set MainWindow DataContext path)
- Modify: `Wabbajack.App.Avalonia/Views/MainWindow.axaml.cs` (navigation)

- [ ] **Step 1: HomeView markup**

`Wabbajack.App.Avalonia/Views/HomeView.axaml` — translate `Views/HomeView.xaml`. Bind to `core:HomeVM` (the DataContext). Replace `Label`→`TextBlock`, `Run`→inline runs, `Hyperlink`→`Button Classes="link"` (or a styled `Button`), `local:BigButton`/`local:LinksView`→the ported controls, `ic:SymbolIcon` (FluentIcons.Avalonia). Use the same brushes (`PrimaryBrush`, `ForegroundBrush`, `ComplementaryPrimary08Brush`, `DarkSecondaryBrush`). Bind the dynamic count `TextBlock`s to `Modlists` via converter/x:Name as in WPF (`Modlists.Length`). Key elements:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ic="using:FluentIcons.Avalonia"
             xmlns:core="using:Wabbajack"
             xmlns:local="using:Wabbajack"
             x:Class="Wabbajack.HomeView"
             x:DataType="core:HomeVM">
    <Grid Margin="8,0,8,0" ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto,Auto,Auto,Auto">
        <TextBlock Grid.Row="0" FontSize="87" FontWeight="Bold">
            <Run Foreground="{DynamicResource ForegroundBrush}" Text="Welcome to " />
            <Run Foreground="{DynamicResource PrimaryBrush}" Text="Wabbajack" />
        </TextBlock>
        <!-- Row 1: tagline; Row 2: the four info cards + LinksView; Row 3: divider; Row 4: Get Started -->
        <local:LinksView Grid.Row="2" Grid.Column="1" Margin="0,16,0,0" />
        <local:BigButton Grid.Row="4" Grid.Column="0" Margin="0,16,16,16" MinHeight="108"
                         ButtonStyle="Color" Title="Get Started" Icon="ChevronRight"
                         Description="Browse the gallery and find yourself a modlist to play"
                         Command="{Binding BrowseCommand}" />
    </Grid>
</UserControl>
```
Fill in rows 1–3 by translating the corresponding WPF `Border`/`Grid`/`TextBlock` blocks from `HomeView.xaml` (lines 35–136) verbatim, swapping `StaticResource`→`DynamicResource` and `<Label>`→`<TextBlock>`. Keep `Hyperlink` actions as small `Button`s bound to `VisitModlistWizardCommand` and `BrowseCommand`.

- [ ] **Step 2: HomeView code-behind**

`Wabbajack.App.Avalonia/Views/HomeView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class HomeView : ReactiveUserControl<HomeVM>
{
    public HomeView() => AvaloniaXamlLoader.Load(this);
}
```
> `HomeVM` is in `Wabbajack.App.Core` (namespace `Wabbajack`).

- [ ] **Step 3: DI host in Program.cs**

Replace `Program.cs` body to build a DI container (mirrors how `Wabbajack.App.Wpf` + the test harness configure services), registering OS-integrated services, the Avalonia abstractions, and the Core ViewModels the Avalonia app uses:
```csharp
using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

internal class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((_, services) =>
            {
                services.AddOSIntegrated();
                services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
                services.AddSingleton<IFileSelector, AvaloniaFileSelector>();
                services.AddSingleton<IDialogService, AvaloniaDialogService>();
                services.AddSingleton<IImageService, AvaloniaImageService>();
                services.AddTransient<HomeVM>(); // Core VMs the Avalonia app currently uses
            }).Build();
        Services = host.Services;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().UseReactiveUI();
}
```
> `ThrowingUserInterventionHandler` is in `Wabbajack.DTOs.Interventions` (Lib). As later waves move VMs into Core, register them here (or introduce a Core-side `AddViewModels`).

- [ ] **Step 4: Navigation in MainWindow**

Replace `MainWindow.axaml.cs` to host the active pane and respond to `NavigateToGlobal`, starting on Home:
```csharp
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.Messages;

namespace Wabbajack.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ActivePane.Content = Program.Services.GetRequiredService<HomeVM>();

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => ActivePane.Content = Resolve(m.Screen)));
    }

    private object Resolve(ScreenType screen) => screen switch
    {
        ScreenType.Home => Program.Services.GetRequiredService<HomeVM>(),
        // Other screens are added as their VMs are ported in later waves.
        _ => ActivePane.Content!
    };
}
```
> The `ViewLocator` turns the `HomeVM` content into a `HomeView`. `NavigateToGlobal`/`ScreenType` are in `Wabbajack.App.Core/Messages`.

- [ ] **Step 5: Build**

Run: `dotnet build Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj -p:EnableWindowsTargeting=true -nologo`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Manual run check (Windows)**

Run: `dotnet run --project Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj -p:EnableWindowsTargeting=true`
Expected: a window opens showing a high-fidelity HomeView; clicking "Get Started" navigates (the content is replaced — for Wave 0 it resolves back to Home since other screens aren't ported, which is fine). Close the window.

- [ ] **Step 7: Commit**

```bash
git add Wabbajack.App.Avalonia
git commit -m "Avalonia Wave 0: HomeView + DI host + navigation; app shows Home on startup"
```

---

### Task 7: Headless test project + first headless UI test

**Files:**
- Create: `Wabbajack.App.Avalonia.Test/Wabbajack.App.Avalonia.Test.csproj`
- Create: `Wabbajack.App.Avalonia.Test/TestApp.cs`
- Create: `Wabbajack.App.Avalonia.Test/HeadlessSession.cs`
- Create: `Wabbajack.App.Avalonia.Test/HomeViewTests.cs`
- Modify: `Wabbajack.sln`

> A dedicated test project keeps the process-global Avalonia headless platform isolated from the main `Wabbajack.Test` suite. It uses TUnit (consistent with the rest of the suite) and Avalonia's framework-agnostic `HeadlessUnitTestSession`.

- [ ] **Step 1: Test csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework Condition=" '$(OS)' == 'Windows_NT'">net10.0-windows</TargetFramework>
    <TargetFramework Condition=" '$(OS)' != 'Windows_NT'">net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.55.2" />
    <PackageReference Include="Avalonia.Headless" Version="11.3.14" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wabbajack.App.Avalonia\Wabbajack.App.Avalonia.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Headless app builder**

`Wabbajack.App.Avalonia.Test/TestApp.cs`:
```csharp
using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;

namespace Wabbajack.App.Avalonia.Test;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<global::Wabbajack.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseReactiveUI();
}
```

- [ ] **Step 3: Shared headless session**

`Wabbajack.App.Avalonia.Test/HeadlessSession.cs`:
```csharp
using System;
using System.Threading.Tasks;
using Avalonia.Headless;

namespace Wabbajack.App.Avalonia.Test;

// One headless Avalonia session per assembly; tests dispatch their bodies onto its UI thread.
public static class HeadlessSession
{
    private static readonly Lazy<HeadlessUnitTestSession> _session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(TestApp)));

    public static Task Dispatch(Func<Task> body) => _session.Value.Dispatch(body, default);
}
```
> Confirm the `HeadlessUnitTestSession.StartNew` / `Dispatch` signatures against `Avalonia.Headless` 11.3.14 (the XUnit/NUnit integrations use this type internally). `StartNew` takes the type exposing a static `BuildAvaloniaApp()`.

- [ ] **Step 4: Write the failing headless test**

`Wabbajack.App.Avalonia.Test/HomeViewTests.cs`:
```csharp
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack;
using Wabbajack.Messages;

namespace Wabbajack.App.Avalonia.Test;

[NotInParallel] // shares the global headless Avalonia platform
public class HomeViewTests
{
    [Test]
    public async Task HomeView_RendersAndBrowseCommandNavigates()
    {
        await HeadlessSession.Dispatch(async () =>
        {
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<HomeVM>.Instance;
            // HomeVM needs ILogger + Client; construct a minimal VM the same way the WJ test harness does,
            // or resolve from a tiny ServiceCollection with AddOSIntegrated(UseLocalCache, UseStubbedGameFolders).
            var vm = TestVm.Home();

            var view = new HomeView { DataContext = vm, ViewModel = vm };
            var window = new global::Avalonia.Controls.Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            // Force a layout pass so the visual tree is realized.
            window.Renderer.Start();

            ScreenType? navigatedTo = null;
            using var sub = MessageBus.Current.Listen<NavigateToGlobal>().Subscribe(m => navigatedTo = m.Screen);

            vm.BrowseCommand.Execute(null);

            await Assert.That(view.IsLoaded).IsTrue();
            await Assert.That(navigatedTo).IsEqualTo(ScreenType.ModListGallery);
        });
    }
}
```
Add a tiny `TestVm` helper (`Wabbajack.App.Avalonia.Test/TestVm.cs`) that builds a `HomeVM` via a minimal DI container:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;
namespace Wabbajack.App.Avalonia.Test;
public static class TestVm
{
    private static readonly System.IServiceProvider _sp = Build();
    private static System.IServiceProvider Build()
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
        s.AddOSIntegrated(o => { o.UseLocalCache = true; o.UseStubbedGameFolders = true; });
        s.AddTransient<global::Wabbajack.HomeVM>();
        return s.BuildServiceProvider();
    }
    public static global::Wabbajack.HomeVM Home() => _sp.GetRequiredService<global::Wabbajack.HomeVM>();
}
```

- [ ] **Step 5: Add to solution + run; verify it passes**

```bash
dotnet sln Wabbajack.sln add Wabbajack.App.Avalonia.Test/Wabbajack.App.Avalonia.Test.csproj
dotnet build Wabbajack.App.Avalonia.Test/Wabbajack.App.Avalonia.Test.csproj -p:EnableWindowsTargeting=true -nologo
```
Run the test exe directly:
`Wabbajack.App.Avalonia.Test/bin/Debug/net10.0-windows/Wabbajack.App.Avalonia.Test.exe`
Expected: `succeeded: 1, failed: 0`. If `HeadlessUnitTestSession`/`HomeView` APIs drift, fix per the package's actual surface (this is the one task most likely to need iteration; budget for it).

- [ ] **Step 6: Commit**

```bash
git add Wabbajack.App.Avalonia.Test Wabbajack.sln
git commit -m "Avalonia Wave 0: headless TUnit UI test for HomeView (render + BrowseCommand navigation)"
```

---

### Task 8: CI wiring + final cross-platform verification

**Files:**
- Modify: `.github/workflows/tests.yaml`

- [ ] **Step 1: Build the Avalonia app + run its headless tests in CI**

In `.github/workflows/tests.yaml`, after the existing test step, add (matching the MTP `dotnet test` style already used):
```yaml
      - name: Build Avalonia app
        run: dotnet build Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj /p:EnableWindowsTargeting=true
      - name: Avalonia headless UI tests
        run: dotnet test --project Wabbajack.App.Avalonia.Test/Wabbajack.App.Avalonia.Test.csproj /p:EnableWindowsTargeting=true
```
> `global.json` already opts `dotnet test` into MTP mode, so no `--` is needed. On the Linux leg the app/tests build and run against `net10.0` (this is the cross-platform proof).

- [ ] **Step 2: Full solution build (Windows + Linux/WSL)**

Run on Windows: `dotnet build Wabbajack.sln -p:EnableWindowsTargeting=true -nologo` → `0 Error(s)`.
Run on Linux/WSL: `dotnet build Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj -nologo` → `0 Error(s)` (proves the Avalonia app builds for `net10.0`).

- [ ] **Step 3: Confirm no regressions in the existing suite**

Run: `dotnet test --project Wabbajack.Test/Wabbajack.Test.csproj -p:EnableWindowsTargeting=true -- --treenode-filter "/*/*/*/*[Category!=RequiresOAuth]"`
Expected: `succeeded: 185, failed: 0, skipped: 2` (unchanged).

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/tests.yaml
git commit -m "Avalonia Wave 0: CI builds the Avalonia app and runs its headless UI tests"
```

---

## Verification (Wave 0 done)

1. `Wabbajack.App.Avalonia` builds on Windows **and** Linux (`net10.0`); full solution builds.
2. Running the app shows a high-fidelity `HomeView`; "Get Started"/links work.
3. The `Avalonia.Headless` TUnit test renders `HomeView` and verifies `BrowseCommand` → `NavigateToGlobal(ModListGallery)`, green locally and in CI.
4. Existing suite unchanged (185 passed / 0 failed / 2 skipped); WPF app still builds and runs.

## Notes for later waves (out of scope here)
- A faithful `ButtonStyle` selector for `BigButton`; image caching/resize parity in `AvaloniaImageService`.
- Move each remaining VM group to Core, port its views/controls + brushes, add headless tests.
- Final wave: delete `Wabbajack.App.Wpf`, rename the Avalonia `AssemblyName` to `Wabbajack`, repoint build/release.
