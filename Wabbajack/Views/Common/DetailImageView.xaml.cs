using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for DetailImageView.xaml
    /// </summary>
    public partial class DetailImageView : UserControlRx
    {
        public ImageSource Image
        {
            get => (ImageSource)GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }
        public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(ImageSource), typeof(DetailImageView),
             new FrameworkPropertyMetadata(default(ImageSource)));

        public ImageSource Badge
        {
            get => (ImageSource)GetValue(BadgeProperty);
            set => SetValue(BadgeProperty, value);
        }
        public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(nameof(Badge), typeof(ImageSource), typeof(DetailImageView),
             new FrameworkPropertyMetadata(default(ImageSource)));

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

        private readonly ObservableAsPropertyHelper<bool> _showAuthor;
        public bool ShowAuthor => _showAuthor.Value;

        private readonly ObservableAsPropertyHelper<bool> _showDescription;
        public bool ShowDescription => _showDescription.Value;

        private readonly ObservableAsPropertyHelper<bool> _showTitle;
        public bool ShowTitle => _showTitle.Value;

        public DetailImageView()
        {
            InitializeComponent();

            _showAuthor = this.WhenAny(x => x.Author)
                .Select(x => !string.IsNullOrWhiteSpace(x))
                .ToProperty(this, nameof(ShowAuthor));

            _showDescription = this.WhenAny(x => x.Description)
                .Select(x => !string.IsNullOrWhiteSpace(x))
                .ToProperty(this, nameof(ShowDescription));

            _showTitle = this.WhenAny(x => x.Title)
                .Select(x => !string.IsNullOrWhiteSpace(x))
                .ToProperty(this, nameof(ShowTitle));
        }
    }
}
