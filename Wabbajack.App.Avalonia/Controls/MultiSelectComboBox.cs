using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Wabbajack;

/// <summary>
/// Avalonia replacement for the SDL WPF Toolkit MultiSelectComboBox used by the gallery filters.
/// Presents the items as a checkable drop-down and exposes the ItemsSource / SelectedItems /
/// SelectedItemsChanged surface the view binds to.
/// </summary>
public class MultiSelectComboBox : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<SelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, SelectionMode>(nameof(SelectionMode), SelectionMode.Multiple);

    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, bool>(nameof(IsEditable));

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, string>(nameof(Watermark), "Any");

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public SelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public IList SelectedItems { get; } = new ObservableCollection<object>();

    public event EventHandler? SelectedItemsChanged;

    private ToggleButton? _toggle;
    private Popup? _popup;
    private ItemsControl? _items;
    private TextBlock? _summary;

    static MultiSelectComboBox()
    {
        ItemsSourceProperty.Changed.AddClassHandler<MultiSelectComboBox>((c, _) => c.RebuildItems());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _toggle = e.NameScope.Find<ToggleButton>("PART_Toggle");
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        _items = e.NameScope.Find<ItemsControl>("PART_Items");
        _summary = e.NameScope.Find<TextBlock>("PART_Summary");

        if (_toggle is not null && _popup is not null)
        {
            _toggle.IsCheckedChanged += (_, _) => _popup.IsOpen = _toggle.IsChecked == true;
            _popup.Closed += (_, _) => _toggle.IsChecked = false;
        }

        RebuildItems();
    }

    private void RebuildItems()
    {
        if (_items is null) return;

        var checkBoxes = new List<Control>();
        foreach (var item in ItemsSource ?? Array.Empty<object>())
        {
            if (item is null) continue;
            var box = new CheckBox
            {
                Content = item.ToString(),
                Tag = item,
                IsChecked = SelectedItems.Contains(item),
                Margin = new Thickness(4, 2),
            };
            box.IsCheckedChanged += OnItemChecked;
            checkBoxes.Add(box);
        }

        _items.ItemsSource = checkBoxes;
        UpdateSummary();
    }

    private void OnItemChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: { } item } box) return;

        if (box.IsChecked == true)
        {
            if (!SelectedItems.Contains(item)) SelectedItems.Add(item);
        }
        else
        {
            SelectedItems.Remove(item);
        }

        UpdateSummary();
        SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSummary()
    {
        if (_summary is null) return;
        _summary.Text = SelectedItems.Count == 0
            ? Watermark
            : string.Join(", ", SelectedItems.Cast<object>().Select(i => i.ToString()));
    }
}
