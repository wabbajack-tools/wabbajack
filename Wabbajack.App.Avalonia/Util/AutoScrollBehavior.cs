using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Wabbajack
{
    internal class AutoScrollBehavior
    {
        private static readonly Dictionary<ListBox, Capture> Associations =
            new Dictionary<ListBox, Capture>();

        public static readonly DependencyProperty ScrollOnNewItemProperty =
            DependencyProperty.RegisterAttached(
                "ScrollOnNewItem",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new UIPropertyMetadata(false, OnScrollOnNewItemChanged));

        public static bool GetScrollOnNewItem(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollOnNewItemProperty);
        }

        public static void SetScrollOnNewItem(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollOnNewItemProperty, value);
        }

        public static void OnScrollOnNewItemChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var listBox = d as ListBox;
            if (listBox == null) return;
            bool oldValue = (bool)e.OldValue, newValue = (bool)e.NewValue;
            if (newValue == oldValue) return;
            if (newValue)
            {
                listBox.Loaded += ListBox_Loaded;
                listBox.Unloaded += ListBox_Unloaded;
                var itemsSourcePropertyDescriptor = TypeDescriptor.GetProperties(listBox)["ItemsSource"];
                itemsSourcePropertyDescriptor.AddValueChanged(listBox, ListBox_ItemsSourceChanged);
            }
            else
            {
                listBox.Loaded -= ListBox_Loaded;
                listBox.Unloaded -= ListBox_Unloaded;
                if (Associations.ContainsKey(listBox))
                    Associations[listBox].Dispose();
                var itemsSourcePropertyDescriptor = TypeDescriptor.GetProperties(listBox)["ItemsSource"];
                itemsSourcePropertyDescriptor.RemoveValueChanged(listBox, ListBox_ItemsSourceChanged);
            }
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
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
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
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            private static bool IsItemVisible(ListBox listBox, object item)
            {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container == null)
                    return false;

                var bounds = container.TransformToAncestor(listBox)
                                      .TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                var viewport = new Rect(0, 0, listBox.ActualWidth, listBox.ActualHeight);
                return viewport.Contains(bounds.TopLeft) || viewport.Contains(bounds.BottomRight);
            }
        }
    }
}
