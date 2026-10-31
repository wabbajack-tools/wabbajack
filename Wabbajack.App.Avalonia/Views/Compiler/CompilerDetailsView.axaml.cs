using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Common;
using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Wabbajack.App.Avalonia.Views.Common;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Views.Compiler;

public partial class CompilerDetailsView : ReactiveUserControl<CompilerDetailsVM>
{
    private bool _userChangingProfileSelection;
    private bool _syncingProfileSelection;

    public CompilerDetailsView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.Settings.ModListName, view => view.ModListNameSetting.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.ModListAuthor, view => view.AuthorNameSetting.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.Version, view => view.VersionSetting.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.ModListDescription, view => view.DescriptionSetting.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.ModListWebsite, view => view.WebsiteSetting.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.ModListReadme, view => view.ReadmeSetting.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.ModListCommunity, view => view.CommunitySetting.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.MachineUrl, view => view.MachineUrl.Text)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Settings.ModlistIsNSFW, view => view.NSFWSetting.IsChecked,
                    b => b, c => c ?? false)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.UseTextureRecompression, view => view.TextureRecompressionSetting.IsChecked,
                    b => b, c => c ?? false)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.AutoGenerateReport, view => view.AutoGenerateReportSetting.IsChecked,
                    b => b, c => c ?? false)
                .DisposeWith(disposables);

            // Each picker is new every time the form is shown, and its path goes both ways with the setting.
            BindPicker(ImageFilePicker, vm => vm.ModListImageLocation, s => s.ModListImage, (s, p) => s.ModListImage = p)
                .DisposeWith(disposables);
            BindPicker(DownloadsPicker, vm => vm.DownloadLocation, s => s.Downloads, (s, p) => s.Downloads = p)
                .DisposeWith(disposables);
            BindPicker(OutputFilePicker, vm => vm.OutputLocation, s => s.OutputFile, (s, p) => s.OutputFile = p)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel!.AvailableProfiles)
                .Subscribe(profiles => ProfileSetting.ItemsSource = profiles)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Settings.Profile, view => view.ProfileSetting.SelectedItem,
                    profile => (object?)profile, item => item as string ?? "")
                .DisposeWith(disposables);

            ViewModel!.WhenAnyValue(v => v.AvailableProfiles, v => v.Settings.Profile)
                .Select(x => (x.Item1 ?? new List<string>()).Except([x.Item2]).ToList())
                .Subscribe(list =>
                {
                    _syncingProfileSelection = true;
                    AdditionalProfilesSetting.ItemsSource = list;
                    _syncingProfileSelection = false;
                    SelectAdditionalProfiles(ViewModel!.Settings.AdditionalProfiles);
                })
                .DisposeWith(disposables);

            AdditionalProfilesSetting.SelectionChanged += OnAdditionalProfilesChanged;
            Disposable.Create(() => AdditionalProfilesSetting.SelectionChanged -= OnAdditionalProfilesChanged)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(vm => vm.Settings.AdditionalProfiles)
                .Subscribe(profiles =>
                {
                    if (_userChangingProfileSelection) return;
                    SelectAdditionalProfiles(profiles);
                })
                .DisposeWith(disposables);
        });
    }

    /// <summary>
    ///     WPF only ever added the settings' profiles to the list's selection; nothing it did took one away.
    /// </summary>
    private void SelectAdditionalProfiles(IEnumerable<string>? profiles)
    {
        if (profiles == null || AdditionalProfilesSetting.SelectedItems is not { } selected) return;
        foreach (var profile in profiles)
        {
            if (AdditionalProfilesSetting.ItemsSource is IEnumerable<string> items && items.Contains(profile) &&
                !selected.Contains(profile))
                selected.Add(profile);
        }
    }

    private void OnAdditionalProfilesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingProfileSelection || ViewModel is not { } vm) return;
        _userChangingProfileSelection = true;
        vm.Settings.AdditionalProfiles = AdditionalProfilesSetting.SelectedItems!.OfType<string>().ToArray();
        _userChangingProfileSelection = false;
    }

    /// <summary>
    ///     What WPF's two Binds per picker did: the picker shows the view model's picker, and its path goes both
    ///     ways with the setting, the setting's value winning when either is replaced.
    /// </summary>
    private IDisposable BindPicker(FilePicker picker, Expression<Func<CompilerDetailsVM, FilePickerVM>> pickerOf,
        Expression<Func<CompilerSettingsVM, AbsolutePath>> setting, Action<CompilerSettingsVM, AbsolutePath> set)
    {
        var subscriptions = new CompositeDisposable();
        var get = setting.Compile();

        this.WhenAnyValue(v => v.ViewModel)
            .Where(vm => vm != null)
            .Select(vm => vm!.WhenAnyValue(pickerOf))
            .Switch()
            .Subscribe(p => picker.PickerVM = p)
            .DisposeWith(subscriptions);

        // Settings to picker.
        picker.WhenAnyValue(p => p.PickerVM)
            .CombineLatest(this.WhenAnyValue(v => v.ViewModel!.Settings), (p, s) => (p, s))
            .Where(x => x.p != null && x.s != null)
            .Select(x => x.s.WhenAnyValue(setting).Select(path => (x.p, path)))
            .Switch()
            .Subscribe(x =>
            {
                if (x.p!.TargetPath != x.path) x.p.TargetPath = x.path;
            })
            .DisposeWith(subscriptions);

        // Picker to settings.
        picker.WhenAnyValue(p => p.PickerVM)
            .Where(p => p != null)
            .Select(p => p!.WhenAnyValue(x => x.TargetPath))
            .Switch()
            .Subscribe(path =>
            {
                if (ViewModel?.Settings is { } s && get(s) != path) set(s, path);
            })
            .DisposeWith(subscriptions);

        return subscriptions;
    }
}
