using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Avalonia.Views.Compiler;

/// <summary>
/// One recently compiled list. Pressing anywhere on it but the delete button opens it, as the WPF tile's
/// MouseDown did; the button handles its own press, so it never reaches the tile.
/// </summary>
public partial class CompiledModListTileView : ReactiveUserControl<CompiledModListTileVM>
{
    public CompiledModListTileView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(v => v.ViewModel!.CompilerSettings.ModListImage)
                .Select(LoadImage)
                .Subscribe(img => ModlistImage.Source = img)
                .DisposeWith(dispose);

            Tile.PointerPressed += OnTilePressed;
            Tile.PointerEntered += UpdateHot;
            Tile.PointerExited += UpdateHot;
            DeleteButton.PointerEntered += UpdateHot;
            DeleteButton.PointerExited += UpdateHot;
            Disposable.Create(() =>
            {
                Tile.PointerPressed -= OnTilePressed;
                Tile.PointerEntered -= UpdateHot;
                Tile.PointerExited -= UpdateHot;
                DeleteButton.PointerEntered -= UpdateHot;
                DeleteButton.PointerExited -= UpdateHot;
            }).DisposeWith(dispose);
        });
    }

    private void OnTilePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        ViewModel?.CompileModListCommand.Execute(null);
    }

    /// <summary>WPF's MultiDataTrigger: over the tile and not over the delete button.</summary>
    private void UpdateHot(object? sender, PointerEventArgs e) =>
        Tile.Classes.Set("hot", Tile.IsPointerOver && !DeleteButton.IsPointerOver);

    private static Bitmap? LoadImage(AbsolutePath path)
    {
        try
        {
            return path != default && path.FileExists() ? new Bitmap(path.ToString()) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
