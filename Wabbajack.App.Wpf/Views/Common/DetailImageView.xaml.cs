using ReactiveUI;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;

namespace Wabbajack;

/// <summary>
/// Interaction logic for DetailImageView.xaml
/// </summary>
public partial class DetailImageView : UserControlRx<ViewModel>
{
    public ImageSource Image
    {
        get => (ImageSource)GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }
    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(ImageSource), typeof(DetailImageView),
         new FrameworkPropertyMetadata(default(ImageSource), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public ImageSource Badge
    {
        get => (ImageSource)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }
    public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(nameof(Badge), typeof(ImageSource), typeof(DetailImageView),
         new FrameworkPropertyMetadata(default(ImageSource), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(DetailImageView),
         new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public string Author
    {
        get => (string)GetValue(AuthorProperty);
        set => SetValue(AuthorProperty, value);
    }
    public static readonly DependencyProperty AuthorProperty = DependencyProperty.Register(nameof(Author), typeof(string), typeof(DetailImageView),
         new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(DetailImageView),
         new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public DetailImageView()
    {
        InitializeComponent();

        this.WhenActivated(dispose =>
        {
            // Update textboxes
            var authorVisible = this.WhenAny(x => x.Author)
                .Select(x => string.IsNullOrWhiteSpace(x) ? Visibility.Collapsed : Visibility.Visible)
                .Replay(1)
                .RefCount();
            authorVisible
                .BindToStrict(this, x => x.AuthorTextBlock.Visibility)
                .DisposeWith(dispose);
            authorVisible
                .BindToStrict(this, x => x.AuthorTextShadow.Visibility)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Author)
                .BindToStrict(this, x => x.AuthorTextRun.Text)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Author)
                .BindToStrict(this, x => x.AuthorShadowTextRun.Text)
                .DisposeWith(dispose);

            /*
            var descVisible = this.WhenAny(x => x.Description)
                .Select(x => string.IsNullOrWhiteSpace(x) ? Visibility.Collapsed : Visibility.Visible)
                .Replay(1)
                .RefCount();
            descVisible
                .BindToStrict(this, x => x.DescriptionTextBlock.Visibility)
                .DisposeWith(dispose);
            descVisible
                .BindToStrict(this, x => x.DescriptionTextShadow.Visibility)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Description)
                .BindToStrict(this, x => x.DescriptionTextBlock.Text)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Description)
                .BindToStrict(this, x => x.DescriptionTextShadow.Text)
                .DisposeWith(dispose);
            */

            var titleVisible = this.WhenAny(x => x.Title)
                .Select(x => string.IsNullOrWhiteSpace(x) ? Visibility.Collapsed : Visibility.Visible)
                .Replay(1)
                .RefCount();
            titleVisible
                .BindToStrict(this, x => x.TitleTextBlock.Visibility)
                .DisposeWith(dispose);
            titleVisible
                .BindToStrict(this, x => x.TitleTextShadow.Visibility)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Title)
                .BindToStrict(this, x => x.TitleTextBlock.Text)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Title)
                .BindToStrict(this, x => x.TitleTextShadow.Text)
                .DisposeWith(dispose);

            // Update other items
            this.WhenAny(x => x.Badge)
                .BindToStrict(this, x => x.BadgeImage.Source)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Image)
                .Select(f => f)
                .BindToStrict(this, x => x.ModlistImage.Source)
                .DisposeWith(dispose);
            this.WhenAny(x => x.Image)
                .Select(img => img == null ? Visibility.Hidden : Visibility.Visible)
                .BindToStrict(this, x => x.Visibility)
                .DisposeWith(dispose);
        });
    }
}
