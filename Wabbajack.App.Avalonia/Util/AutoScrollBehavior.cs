using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Wabbajack
{
    internal class AutoScrollBehavior
    {
        private static readonly Dictionary<ListBox, Capture> Associations =
            new Dictionary<ListBox, Capture>();

        public static readonly AttachedProperty<bool> ScrollOnNewItemProperty =
            AvaloniaProperty.RegisterAttached<AutoScrollBehavior, ListBox, bool>(
                "ScrollOnNewItem",
                false);

        static AutoScrollBehavior()
        {
            ScrollOnNewItemProperty.Changed.Subscribe(e =>
                OnScrollOnNewItemChanged((AvaloniaObject)e.Sender, e));
        }

        public static bool GetScrollOnNewItem(AvaloniaObject obj)
        {
            return obj.GetValue(ScrollOnNewItemProperty);
        }

        public static void SetScrollOnNewItem(AvaloniaObject obj, bool value)
        {
            obj.SetValue(ScrollOnNewItemProperty, value);
        }

        public static void OnScrollOnNewItemChanged(
            AvaloniaObject d,
            AvaloniaPropertyChangedEventArgs e)
        {
            var listBox = d as ListBox;
            if (listBox == null) return;
            bool oldValue = (bool)e.OldValue!, newValue = (bool)e.NewValue!;
            if (newValue == oldValue) return;
            if (newValue)
            {
                listBox.Loaded += ListBox_Loaded;
                listBox.Unloaded += ListBox_Unloaded;
                // WPF used TypeDescriptor.AddValueChanged on the ItemsSource CLR property;
                // the Avalonia equivalent is listening for AvaloniaProperty changes directly.
                listBox.PropertyChanged += ListBox_PropertyChanged;
            }
            else
            {
                listBox.Loaded -= ListBox_Loaded;
                listBox.Unloaded -= ListBox_Unloaded;
                if (Associations.ContainsKey(listBox))
                    Associations[listBox].Dispose();
                listBox.PropertyChanged -= ListBox_PropertyChanged;
            }
        }

        private static void ListBox_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != ItemsControl.ItemsSourceProperty) return;
            if (sender is not ListBox listBox) return;
            ListBox_ItemsSourceChanged(listBox, EventArgs.Empty);
        }

        private static void ListBox_ItemsSourceChanged(object sender, EventArgs e)
        {
            var listBox = (ListBox)sender;
            if (Associations.ContainsKey(listBox))
                Associations[listBox].Dispose();
            Associations[listBox] = new Capture(listBox);
        }

        private static void ListBox_Unloaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (Associations.ContainsKey(listBox))
                Associations[listBox].Dispose();
            listBox.Unloaded -= ListBox_Unloaded;
        }

        private static void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;
            var incc = listBox.Items as INotifyCollectionChanged;
            if (incc == null) return;
            listBox.Loaded -= ListBox_Loaded;
            Associations[listBox] = new Capture(listBox);
        }

        private class Capture : IDisposable
        {
            private readonly INotifyCollectionChanged _incc;
            private readonly ListBox _listBox;
            private DateTime _lastScrollTime = DateTime.MinValue;
            private readonly TimeSpan _throttleInterval = TimeSpan.FromMilliseconds(100);

            public Capture(ListBox listBox)
            {
                _listBox = listBox;
                _incc = listBox.ItemsSource as INotifyCollectionChanged;
                if (_incc != null)
                    _incc.CollectionChanged += incc_CollectionChanged;
            }

            public void Dispose()
            {
                if (_incc != null)
                    _incc.CollectionChanged -= incc_CollectionChanged;
            }

            private void incc_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null || e.NewItems.Count == 0)
                    return;

                // Throttle to avoid layout storms
                var now = DateTime.Now;
                if (now - _lastScrollTime < _throttleInterval)
                    return;

                _lastScrollTime = now;

                // Defer to Dispatcher to ensure layout has completed
                Dispatcher.UIThread.Post(() =>
                {
                    var item = e.NewItems[0];

                    // Avoid triggering if item is already in view
                    if (IsItemVisible(_listBox, item))
                        return;

                    try
                    {
                        _listBox.ScrollIntoView(item);
                        _listBox.SelectedItem = item;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Safe fallback
                    }
                }, DispatcherPriority.Background);
            }

            private static bool IsItemVisible(ListBox listBox, object item)
            {
                var container = listBox.ContainerFromItem(item) as Control;
                if (container == null)
                    return false;

                var transform = container.TransformToVisual(listBox);
                if (transform == null)
                    return false;

                var bounds = new Rect(0, 0, container.Bounds.Width, container.Bounds.Height)
                    .TransformToAABB(transform.Value);
                var viewport = new Rect(0, 0, listBox.Bounds.Width, listBox.Bounds.Height);
                return viewport.Contains(bounds.TopLeft) || viewport.Contains(bounds.BottomRight);
            }
        }
    }
}
