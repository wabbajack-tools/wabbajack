using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Wabbajack.App.Avalonia.Views.Common;

/// <summary>
/// The WPF DetailImageView: a modlist image with its title, version and author laid over the bottom. Each
/// line hides when it has nothing to say, and the two font sizes are the caller's.
/// </summary>
public partial class DetailImageView : UserControl
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<DetailImageView, IImage?>(nameof(Image));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DetailImageView, string?>(nameof(Title));

    public static readonly StyledProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.Register<DetailImageView, double>(nameof(TitleFontSize));

    public static readonly StyledProperty<string?> AuthorProperty =
        AvaloniaProperty.Register<DetailImageView, string?>(nameof(Author));

    public static readonly StyledProperty<double> AuthorFontSizeProperty =
        AvaloniaProperty.Register<DetailImageView, double>(nameof(AuthorFontSize));

    public static readonly StyledProperty<Version?> VersionProperty =
        AvaloniaProperty.Register<DetailImageView, Version?>(nameof(Version));

    public DetailImageView()
    {
        InitializeComponent();
    }

    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public double TitleFontSize
    {
        get => GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    public string? Author
    {
        get => GetValue(AuthorProperty);
        set => SetValue(AuthorProperty, value);
    }

    public double AuthorFontSize
    {
        get => GetValue(AuthorFontSizeProperty);
        set => SetValue(AuthorFontSizeProperty, value);
    }

    public Version? Version
    {
        get => GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ImageProperty)
        {
            ModlistImage.Source = Image;
            ModlistImage.IsVisible = Image != null;
        }
        else if (change.Property == TitleProperty)
        {
            TitleTextBlock.Text = Title;
            TitleTextBlock.IsVisible = !string.IsNullOrWhiteSpace(Title);
        }
        else if (change.Property == AuthorProperty)
        {
            AuthorText.Text = Author;
            AuthorText.IsVisible = AuthorPrefixText.IsVisible = !string.IsNullOrWhiteSpace(Author);
        }
        else if (change.Property == VersionProperty)
        {
            var text = Version?.ToString();
            VersionText.Text = text ?? string.Empty;
            VersionText.IsVisible = VersionPrefixText.IsVisible = !string.IsNullOrWhiteSpace(text);
        }
        // Unset, the sizes are left to the TextBlock style rather than set to nothing.
        else if (change.Property == TitleFontSizeProperty)
        {
            SetSize(TitleTextBlock, TitleFontSize);
        }
        else if (change.Property == AuthorFontSizeProperty)
        {
            foreach (var text in new[] { VersionPrefixText, VersionText, AuthorPrefixText, AuthorText })
                SetSize(text, AuthorFontSize);
        }
    }

    private static void SetSize(TextBlock text, double size)
    {
        if (size > 0) text.FontSize = size;
        else text.ClearValue(FontSizeProperty);
    }
}
