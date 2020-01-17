using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using Wabbajack.Lib;

namespace Wabbajack
{
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

        [Reactive]
        public bool ShowAuthor { get; private set; }

        [Reactive]
        public bool ShowDescription { get; private set; }

        [Reactive]
        public bool ShowTitle { get; private set; }

        public DetailImageView()
        {
            InitializeComponent();

            this.WhenActivated(dispose =>
            {
                this.WhenAny(x => x.Author)
                    .Select(x => !string.IsNullOrWhiteSpace(x))
                    .Subscribe(x => ShowAuthor = x)
                    .DisposeWith(dispose);

                this.WhenAny(x => x.Description)
                    .Select(x => !string.IsNullOrWhiteSpace(x))
                    .Subscribe(x => ShowDescription = x)
                    .DisposeWith(dispose);

                this.WhenAny(x => x.Title)
                    .Select(x => !string.IsNullOrWhiteSpace(x))
                    .Subscribe(x => ShowTitle = x)
                    .DisposeWith(dispose);
            });
        }
    }
}
