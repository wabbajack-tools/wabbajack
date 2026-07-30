using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for DetailImageView.axaml
/// </summary>
public partial class DetailImageView : ReactiveUserControl<ViewModel>
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<DetailImageView, IImage?>(nameof(Image), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DetailImageView, string?>(nameof(Title), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.Register<DetailImageView, double>(nameof(TitleFontSize), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public double TitleFontSize
    {
        get => GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    public static readonly StyledProperty<string?> AuthorProperty =
        AvaloniaProperty.Register<DetailImageView, string?>(nameof(Author), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? Author
    {
        get => GetValue(AuthorProperty);
        set => SetValue(AuthorProperty, value);
    }

    public static readonly StyledProperty<double> AuthorFontSizeProperty =
        AvaloniaProperty.Register<DetailImageView, double>(nameof(AuthorFontSize), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public double AuthorFontSize
    {
        get => GetValue(AuthorFontSizeProperty);
        set => SetValue(AuthorFontSizeProperty, value);
    }

    public static readonly StyledProperty<Version?> VersionProperty =
        AvaloniaProperty.Register<DetailImageView, Version?>(nameof(Version), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public Version? Version
    {
        get => GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public DetailImageView()
    {
        InitializeComponent();

        this.WhenActivated(dispose =>
        {
            // Update textboxes
            // NOTE: WPF's UserControlRx<T> manually bridged DependencyProperty changes into
            // INotifyPropertyChanged.PropertyChanged (via WireNotifyPropertyChanged) so that
            // this.WhenAnyValue(...) could observe them. In Avalonia, AvaloniaObject already
            // raises INotifyPropertyChanged.PropertyChanged for every AvaloniaProperty change,
            // so WhenAnyValue works on the StyledProperty-backed CLR properties below without
            // any extra plumbing.
            var authorVisible = this.WhenAnyValue(x => x.Author)
                .Select(x => !string.IsNullOrWhiteSpace(x))
                .Replay(1)
                .RefCount();
            authorVisible
                .BindToStrict(this, x => x.AuthorText.IsVisible)
                .DisposeWith(dispose);
            authorVisible
                .BindToStrict(this, x => x.AuthorPrefixText.IsVisible)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.Author)
                .BindToStrict(this, x => x.AuthorText.Text)
                .DisposeWith(dispose);

            var titleVisible = this.WhenAnyValue(x => x.Title)
                .Select(x => !string.IsNullOrWhiteSpace(x))
                .Replay(1)
                .RefCount();
            titleVisible
                .BindToStrict(this, x => x.TitleTextBlock.IsVisible)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.Title)
                .BindToStrict(this, x => x.TitleTextBlock.Text)
                .DisposeWith(dispose);

            var versionVisible = this.WhenAny(x => x.Version)
                .Select(x => x?.ToString())
                .Select(x => !string.IsNullOrWhiteSpace(x))
                .Replay(1)
                .RefCount();
            versionVisible
                .BindToStrict(this, x => x.VersionText.IsVisible)
                .DisposeWith(dispose);
            versionVisible
                .BindToStrict(this, x => x.VersionPrefixText.IsVisible)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Version)
                .Select(x => x != null ? x.ToString() : string.Empty)
                .BindToStrict(this, x => x.VersionText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Image)
                .Select(f => f)
                .BindToStrict(this, x => x.ModlistImage.Source)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.Image)
                .Select(img => img != null)
                .BindToStrict(this, x => x.ModlistImage.IsVisible)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.TitleFontSize)
                .BindToStrict(this, x => x.TitleTextBlock.FontSize)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.AuthorFontSize)
                .BindToStrict(this, x => x.VersionPrefixText.FontSize)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.AuthorFontSize)
                .BindToStrict(this, x => x.VersionText.FontSize)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.AuthorFontSize)
                .BindToStrict(this, x => x.AuthorPrefixText.FontSize)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.AuthorFontSize)
                .BindToStrict(this, x => x.AuthorText.FontSize)
                .DisposeWith(dispose);
        });
    }
}
