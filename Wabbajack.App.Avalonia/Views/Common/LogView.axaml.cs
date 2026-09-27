using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Common;
using static Wabbajack.App.Avalonia.ViewModels.Common.LogStream;

namespace Wabbajack.App.Avalonia.Views.Common;

/// <summary>
/// The WPF LogView. WPF found the log through the DataContext (<c>LoggerProvider.MessageLog</c> on the
/// installer or compiler view model); here the host binds it: <c>LoggerProvider="{Binding LoggerProvider}"</c>.
/// It shows messages at Info and above, as WPF's CollectionViewSource filter did, and follows new ones the way
/// WPF's AutoScrollBehavior did: at most every 100ms, scroll to the newest and select it unless it is already
/// in view.
/// </summary>
public partial class LogView : UserControl
{
    public static readonly StyledProperty<LogStream?> LoggerProviderProperty =
        AvaloniaProperty.Register<LogView, LogStream?>(nameof(LoggerProvider));

    private readonly SerialDisposable _subscription = new();
    private DateTime _lastScrollTime = DateTime.MinValue;
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromMilliseconds(100);

    public LogView()
    {
        InitializeComponent();
    }

    public LogStream? LoggerProvider
    {
        get => GetValue(LoggerProviderProperty);
        set => SetValue(LoggerProviderProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _subscription.Disposable = null;
        LogList.ItemsSource = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LoggerProviderProperty && VisualRoot != null)
            Subscribe();
    }

    private void Subscribe()
    {
        if (LoggerProvider is not { } log)
        {
            _subscription.Disposable = null;
            LogList.ItemsSource = null;
            return;
        }

        var disposables = new CompositeDisposable();
        log.MessageLog
            .ToObservableChangeSet()
            .Filter(m => m.Level.Ordinal >= 2)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out ReadOnlyObservableCollection<ILogMessage> shown)
            .Subscribe()
            .DisposeWith(disposables);

        ((INotifyCollectionChanged)shown).CollectionChanged += OnCollectionChanged;
        Disposable.Create(() => ((INotifyCollectionChanged)shown).CollectionChanged -= OnCollectionChanged)
            .DisposeWith(disposables);

        LogList.ItemsSource = shown;
        _subscription.Disposable = disposables;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null || e.NewItems.Count == 0)
            return;

        // Throttled to avoid layout storms.
        var now = DateTime.Now;
        if (now - _lastScrollTime < ThrottleInterval)
            return;
        _lastScrollTime = now;

        var item = e.NewItems[0];
        // After layout has caught up with the new rows.
        Dispatcher.UIThread.Post(() =>
        {
            if (item == null || IsItemVisible(item)) return;
            LogList.ScrollIntoView(item);
            LogList.SelectedItem = item;
        }, DispatcherPriority.Background);
    }

    /// <summary>Whether either corner of the item's row is inside the list, as WPF judged it.</summary>
    private bool IsItemVisible(object item)
    {
        if (LogList.ContainerFromItem(item) is not { IsVisible: true } container)
            return false;

        var topLeft = container.TranslatePoint(new Point(0, 0), LogList);
        var bottomRight = container.TranslatePoint(new Point(container.Bounds.Width, container.Bounds.Height), LogList);
        if (topLeft == null || bottomRight == null) return false;

        var viewport = new Rect(LogList.Bounds.Size);
        return viewport.Contains(topLeft.Value) || viewport.Contains(bottomRight.Value);
    }
}
