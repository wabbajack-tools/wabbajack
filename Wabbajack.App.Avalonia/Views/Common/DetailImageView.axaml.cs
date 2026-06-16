using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Wabbajack;

// No view model: the parent sets the StyledProperties directly.
// Image is an Avalonia IImage (e.g. Bitmap); Title/Author are strings; Version is a System.Version.
public partial class DetailImageView : UserControl
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<DetailImageView, IImage?>(nameof(Image));
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DetailImageView, string?>(nameof(Title));
    public static readonly StyledProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.Register<DetailImageView, double>(nameof(TitleFontSize), 20d);
    public static readonly StyledProperty<string?> AuthorProperty =
        AvaloniaProperty.Register<DetailImageView, string?>(nameof(Author));
    public static readonly StyledProperty<double> AuthorFontSizeProperty =
        AvaloniaProperty.Register<DetailImageView, double>(nameof(AuthorFontSize), 12d);
    public static readonly StyledProperty<Version?> VersionProperty =
        AvaloniaProperty.Register<DetailImageView, Version?>(nameof(Version));

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public double TitleFontSize { get => GetValue(TitleFontSizeProperty); set => SetValue(TitleFontSizeProperty, value); }
    public string? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }
    public double AuthorFontSize { get => GetValue(AuthorFontSizeProperty); set => SetValue(AuthorFontSizeProperty, value); }
    public Version? Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }

    public DetailImageView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
