using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.App.Avalonia.ViewModels.Gallery;

namespace Wabbajack.App.Avalonia.Views.Gallery;

public partial class ModListTileView : ReactiveUserControl<BaseModListMetadataVM>
{
    private static readonly BlurEffect BrokenBlur = new() { Radius = 25 };

    public ModListTileView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(v => v.ViewModel!.Metadata.DownloadMetadata)
                .Subscribe(sizes =>
                {
                    DownloadSizeRun.Text = UIUtils.FormatBytes(sizes?.SizeOfArchives ?? 0, round: true);
                    InstallSizeRun.Text = UIUtils.FormatBytes(sizes?.SizeOfInstalledFiles ?? 0, round: true);
                })
                .DisposeWith(disposables);

            ModListTile.PointerEntered += OnHover;
            ModListTile.PointerExited += OnHover;
            Disposable.Create(() =>
            {
                ModListTile.PointerEntered -= OnHover;
                ModListTile.PointerExited -= OnHover;
            }).DisposeWith(disposables);

            Apply(false);
        });
    }

    private void OnHover(object? sender, PointerEventArgs e) => Apply(ModListTile.IsPointerOver);

    /// <summary>What WPF's IsMouseOver triggers on ModListTile switched, in one place.</summary>
    private void Apply(bool hot)
    {
        if (ViewModel is not { } vm) return;

        ModListTile.Classes.Set("hot", hot);
        GameNamePill.IsVisible = hot;
        TitleOverlay.IsVisible = !vm.ImageContainsTitle || hot;

        var broken = hot && vm.IsBroken;
        ImageHost.Effect = broken ? BrokenBlur : null;
        BrokenShade.IsVisible = broken;
        BrokenText.IsVisible = broken;
    }
}
