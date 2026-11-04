using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Wabbajack.App.Avalonia.Controls;

/// <summary>
/// One entry of a <see cref="MultiSelectComboBox" />, as the drop-down list and the selected-items panel draw
/// it. Items are shown by their ToString(), as Sdl's control showed them.
/// </summary>
public sealed class MultiSelectComboBoxItem : INotifyPropertyChanged
{
    private bool _isChecked;

    internal MultiSelectComboBoxItem(object item)
    {
        Item = item;
        Text = item.ToString() ?? string.Empty;
    }

    public object Item { get; }

    public string Text { get; }

    public bool IsChecked
    {
        get => _isChecked;
        internal set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // The list box's type-to-search reads this.
    public override string ToString() => Text;
}

/// <summary>
/// The slot after the last selected item that holds the filter box. Sdl kept a null in the same place.
/// </summary>
public sealed class MultiSelectComboBoxFilterSlot
{
    internal static readonly MultiSelectComboBoxFilterSlot Instance = new();

    private MultiSelectComboBoxFilterSlot()
    {
    }
}

public sealed class MultiSelectComboBoxSelectionChangedEventArgs : RoutedEventArgs
{
    public MultiSelectComboBoxSelectionChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<object> added,
        IReadOnlyList<object> removed, IReadOnlyList<object> selected) : base(routedEvent)
    {
        Added = added;
        Removed = removed;
        Selected = selected;
    }

    public IReadOnlyList<object> Added { get; }
    public IReadOnlyList<object> Removed { get; }
    public IReadOnlyList<object> Selected { get; }
}

/// <summary>
/// The Sdl.MultiSelectComboBox the WPF gallery uses for "Has mod(s)" and "Has tag(s)", in multiple-selection
/// mode: the selected items sit in a wrapping row with a remove button each, a filter box after them narrows
/// the drop-down list as the user types, and clicking a row in the list toggles it. The look is in
/// Themes/MultiSelectComboBox.axaml.
/// <para>
/// The control starts read-only: a click (or F2) puts it in edit mode, which shows the filter box, the remove
/// buttons and a chevron in place of the pencil. It leaves edit mode half a second after focus has left it.
/// </para>
/// <para>
/// <see cref="SelectedItems" /> is written in place when the user changes the selection, and changes made to
/// it from outside (or a new list) are picked up and reported. <see cref="SelectedItemsChanged" /> is raised
/// after the fact, on the dispatcher, as Sdl raised it.
/// </para>
/// </summary>
[TemplatePart(PART_Root, typeof(Control))]
[TemplatePart(PART_SelectedItemsHost, typeof(Control))]
[TemplatePart(PART_SelectedItemsPanel, typeof(ItemsControl))]
[TemplatePart(PART_DropDownButton, typeof(Button))]
[TemplatePart(PART_Popup, typeof(Popup))]
[TemplatePart(PART_DropDownList, typeof(ListBox))]
[PseudoClasses(":editmode", ":dropdownopen", ":noresults")]
public class MultiSelectComboBox : TemplatedControl
{
    private const string PART_Root = "PART_Root";
    private const string PART_SelectedItemsHost = "PART_SelectedItemsHost";
    private const string PART_SelectedItemsPanel = "PART_SelectedItemsPanel";
    private const string PART_DropDownButton = "PART_DropDownButton";
    private const string PART_Popup = "PART_Popup";
    private const string PART_DropDownList = "PART_DropDownList";
    private const string PART_FilterTextBox = "PART_FilterTextBox";
    private const string PART_RemoveItemButton = "PART_RemoveItemButton";

    // Sdl waited this long after focus left before closing edit mode, so focus moving into the drop-down
    // (which it does on every click there) does not end it.
    private static readonly TimeSpan EditModeCloseDelay = TimeSpan.FromMilliseconds(500);

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly DirectProperty<MultiSelectComboBox, IList?> SelectedItemsProperty =
        AvaloniaProperty.RegisterDirect<MultiSelectComboBox, IList?>(nameof(SelectedItems),
            o => o.SelectedItems, (o, v) => o.SelectedItems = v, defaultBindingMode: BindingMode.TwoWay);

    // Sdl's default is true; the app's style turns it off and the gallery turns it back on, which the
    // theme file repeats.
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, bool>(nameof(IsEditable), true);

    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, bool>(nameof(IsDropDownOpen),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, double>(nameof(MaxDropDownHeight), 360);

    public static readonly StyledProperty<string> RemoveToolTipStringProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, string>(nameof(RemoveToolTipString), "Remove");

    public static readonly DirectProperty<MultiSelectComboBox, string> FilterTextProperty =
        AvaloniaProperty.RegisterDirect<MultiSelectComboBox, string>(nameof(FilterText),
            o => o.FilterText, (o, v) => o.FilterText = v, string.Empty, BindingMode.TwoWay);

    public static readonly DirectProperty<MultiSelectComboBox, bool> IsEditModeProperty =
        AvaloniaProperty.RegisterDirect<MultiSelectComboBox, bool>(nameof(IsEditMode), o => o.IsEditMode);

    public static readonly DirectProperty<MultiSelectComboBox, bool> CanRemoveItemsProperty =
        AvaloniaProperty.RegisterDirect<MultiSelectComboBox, bool>(nameof(CanRemoveItems), o => o.CanRemoveItems);

    public static readonly DirectProperty<MultiSelectComboBox, bool> IsFilterBoxInLayoutProperty =
        AvaloniaProperty.RegisterDirect<MultiSelectComboBox, bool>(nameof(IsFilterBoxInLayout),
            o => o.IsFilterBoxInLayout);

    public static readonly RoutedEvent<MultiSelectComboBoxSelectionChangedEventArgs> SelectedItemsChangedEvent =
        RoutedEvent.Register<MultiSelectComboBox, MultiSelectComboBoxSelectionChangedEventArgs>(
            nameof(SelectedItemsChanged), RoutingStrategies.Bubble);

    // Every item of ItemsSource, in order, and a wrapper for every item ever shown, so a selected item that
    // has left ItemsSource keeps its chip.
    private readonly List<MultiSelectComboBoxItem> _allItems = new();
    private readonly Dictionary<object, MultiSelectComboBoxItem> _wrappers = new();

    // The selection in the order it was made, and what the selected-items panel draws: the same items, then
    // the filter slot.
    private readonly List<object> _selected = new();
    private readonly AvaloniaList<object> _chips = new() { MultiSelectComboBoxFilterSlot.Instance };

    private IList? _selectedItems;
    private INotifyCollectionChanged? _observedSelectedItems;
    private INotifyCollectionChanged? _observedItemsSource;
    private bool _writingSelectedItems;
    private bool _syncPending;

    private string _filterText = string.Empty;
    private bool _isEditMode;
    private bool _canRemoveItems;
    private bool _isFilterBoxInLayout;
    private IReadOnlyList<MultiSelectComboBoxItem> _filteredItems = Array.Empty<MultiSelectComboBoxItem>();

    private Control? _root;
    private Control? _selectedItemsHost;
    private ItemsControl? _selectedItemsPanel;
    private Button? _dropDownButton;
    private Popup? _popup;
    private ListBox? _list;

    private TopLevel? _topLevel;
    private MultiSelectComboBoxItem? _rangeAnchor;
    private bool _backspaceRemoved;

    public MultiSelectComboBox()
    {
        SelectedItems = new ObservableCollection<object>();

        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpBubble, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(LostFocusEvent, OnAnyLostFocus, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// The selected items. The control writes the user's changes into this list, and follows it when it is
    /// replaced or (if it notifies) changed from outside. Null is taken as a new empty list.
    /// </summary>
    public IList? SelectedItems
    {
        get => _selectedItems;
        set
        {
            value ??= new ObservableCollection<object>();
            var old = _selectedItems;
            if (!SetAndRaise(SelectedItemsProperty, ref _selectedItems, value)) return;

            if (_observedSelectedItems != null)
                _observedSelectedItems.CollectionChanged -= OnSelectedItemsCollectionChanged;
            _observedSelectedItems = value as INotifyCollectionChanged;
            if (_observedSelectedItems != null)
                _observedSelectedItems.CollectionChanged += OnSelectedItemsCollectionChanged;

            if (old != null) SyncFromSelectedItems();
        }
    }

    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public double MaxDropDownHeight
    {
        get => GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public string RemoveToolTipString
    {
        get => GetValue(RemoveToolTipStringProperty);
        set => SetValue(RemoveToolTipStringProperty, value);
    }

    /// <summary>What is typed in the filter box. The drop-down list shows the items whose text contains it.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetAndRaise(FilterTextProperty, ref _filterText, value ?? string.Empty))
                ApplyFilter();
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (!SetAndRaise(IsEditModeProperty, ref _isEditMode, value)) return;
            PseudoClasses.Set(":editmode", value);
            UpdateCanRemoveItems();
        }
    }

    /// <summary>Whether the remove buttons show: only in edit mode, and only when the control is editable.</summary>
    public bool CanRemoveItems
    {
        get => _canRemoveItems;
        private set => SetAndRaise(CanRemoveItemsProperty, ref _canRemoveItems, value);
    }

    /// <summary>
    /// Whether the filter box takes up space. Sdl kept it collapsed until the first edit and only hid it
    /// afterwards, so the row keeps its width once the user has typed in it.
    /// </summary>
    public bool IsFilterBoxInLayout
    {
        get => _isFilterBoxInLayout;
        private set => SetAndRaise(IsFilterBoxInLayoutProperty, ref _isFilterBoxInLayout, value);
    }

    /// <summary>The selected items joined with ", ", which is what Ctrl+C in the filter box copies.</summary>
    public string SelectedItemsAsText => string.Join(", ", _selected.Select(i => i.ToString()));

    public event EventHandler<MultiSelectComboBoxSelectionChangedEventArgs>? SelectedItemsChanged
    {
        add => AddHandler(SelectedItemsChangedEvent, value);
        remove => RemoveHandler(SelectedItemsChangedEvent, value);
    }

    private MultiSelectComboBoxItem? CurrentItem => _list?.SelectedItem as MultiSelectComboBoxItem;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_list != null)
        {
            _list.RemoveHandler(KeyDownEvent, OnListKeyDown);
            _list.RemoveHandler(PointerPressedEvent, OnListPointerPressed);
            _list.RemoveHandler(PointerReleasedEvent, OnListPointerReleased);
            _list.RemoveHandler(LostFocusEvent, OnAnyLostFocus);
        }

        _root = e.NameScope.Find<Control>(PART_Root);
        _selectedItemsHost = e.NameScope.Find<Control>(PART_SelectedItemsHost);
        _selectedItemsPanel = e.NameScope.Find<ItemsControl>(PART_SelectedItemsPanel);
        _dropDownButton = e.NameScope.Find<Button>(PART_DropDownButton);
        _popup = e.NameScope.Find<Popup>(PART_Popup);
        _list = e.NameScope.Find<ListBox>(PART_DropDownList);

        if (_selectedItemsPanel != null)
            _selectedItemsPanel.ItemsSource = _chips;

        if (_list != null)
        {
            // The list only tracks which row the keyboard is on; checking rows is this control's job.
            _list.SelectionMode = SelectionMode.Single;
            _list.ItemsSource = _filteredItems;
            _list.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
            _list.AddHandler(PointerPressedEvent, OnListPointerPressed, RoutingStrategies.Tunnel,
                handledEventsToo: true);
            _list.AddHandler(PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Tunnel,
                handledEventsToo: true);
            _list.AddHandler(LostFocusEvent, OnAnyLostFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        ApplyFilter();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (IsDropDownOpen) WatchOutside(true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        WatchOutside(false);
        _topLevel = null;
        IsDropDownOpen = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            if (_observedItemsSource != null)
                _observedItemsSource.CollectionChanged -= OnItemsSourceCollectionChanged;
            _observedItemsSource = ItemsSource as INotifyCollectionChanged;
            if (_observedItemsSource != null)
                _observedItemsSource.CollectionChanged += OnItemsSourceCollectionChanged;
            RebuildItems();
        }
        else if (change.Property == IsEditableProperty)
        {
            UpdateCanRemoveItems();
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(":dropdownopen", open);
            WatchOutside(open);
            if (open)
            {
                // Sdl put the keyboard on the first row whenever the list opened.
                SetCurrentItem(_filteredItems.Count > 0 ? 0 : -1);
            }
            else
            {
                // And gave the filter box the keyboard back when it closed.
                FocusFilterBox();
            }
        }
    }

    // ---- Selection ----------------------------------------------------------------------------------

    private MultiSelectComboBoxItem GetWrapper(object item)
    {
        if (!_wrappers.TryGetValue(item, out var wrapper))
        {
            wrapper = new MultiSelectComboBoxItem(item);
            _wrappers[item] = wrapper;
        }

        return wrapper;
    }

    private void RebuildItems()
    {
        _allItems.Clear();
        if (ItemsSource != null)
        {
            foreach (var item in ItemsSource)
            {
                if (item == null) continue;
                var wrapper = GetWrapper(item);
                wrapper.IsChecked = _selected.Contains(item);
                _allItems.Add(wrapper);
            }
        }

        ApplyFilter();
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildItems();

    private void Select(object item) => ChangeSelection(new[] { item }, Array.Empty<object>());

    private void Deselect(object item) => ChangeSelection(Array.Empty<object>(), new[] { item });

    private void Toggle(MultiSelectComboBoxItem row)
    {
        if (_selected.Contains(row.Item)) Deselect(row.Item);
        else Select(row.Item);
    }

    /// <summary>A change the user made: applied here, written into <see cref="SelectedItems" />, then reported.</summary>
    private void ChangeSelection(IReadOnlyList<object> add, IReadOnlyList<object> remove)
    {
        var added = add.Where(i => !_selected.Contains(i)).Distinct().ToList();
        var removed = remove.Where(i => _selected.Contains(i)).Distinct().ToList();
        if (added.Count == 0 && removed.Count == 0) return;

        ApplySelection(added, removed);
        WriteSelectedItems();
        RaiseSelectedItemsChanged(added, removed);
    }

    private void ApplySelection(IReadOnlyList<object> added, IReadOnlyList<object> removed)
    {
        foreach (var item in removed)
        {
            _selected.Remove(item);
            var wrapper = GetWrapper(item);
            wrapper.IsChecked = false;
            _chips.Remove(wrapper);
        }

        foreach (var item in added)
        {
            _selected.Add(item);
            var wrapper = GetWrapper(item);
            wrapper.IsChecked = true;
            // Before the filter slot, which stays last.
            _chips.Insert(_chips.Count - 1, wrapper);
        }
    }

    private void WriteSelectedItems()
    {
        var list = _selectedItems;
        if (list == null || list.IsReadOnly || list.IsFixedSize) return;

        _writingSelectedItems = true;
        try
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!_selected.Contains(list[i]!))
                    list.RemoveAt(i);
            }

            foreach (var item in _selected)
            {
                if (!list.Contains(item))
                    list.Add(item);
            }
        }
        finally
        {
            _writingSelectedItems = false;
        }
    }

    private void OnSelectedItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_writingSelectedItems || _syncPending) return;

        // Let a caller make several changes and take them in one go, as Sdl did.
        _syncPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _syncPending = false;
            SyncFromSelectedItems();
        }, DispatcherPriority.Background);
    }

    /// <summary>A change made from outside, by replacing or editing <see cref="SelectedItems" />.</summary>
    private void SyncFromSelectedItems()
    {
        var wanted = (_selectedItems ?? Array.Empty<object>()).Cast<object?>()
            .Where(i => i != null).Select(i => i!).Distinct().ToList();

        var removed = _selected.Where(i => !wanted.Contains(i)).ToList();
        var added = wanted.Where(i => !_selected.Contains(i)).ToList();
        if (added.Count == 0 && removed.Count == 0) return;

        ApplySelection(added, removed);
        RaiseSelectedItemsChanged(added, removed);
    }

    private void RaiseSelectedItemsChanged(IReadOnlyList<object> added, IReadOnlyList<object> removed)
    {
        var selected = _selected.ToList();

        // Raised after the fact rather than from inside the change, so a handler that replaces
        // SelectedItems (as the gallery does) is not re-entering it.
        Dispatcher.UIThread.Post(() =>
            RaiseEvent(new MultiSelectComboBoxSelectionChangedEventArgs(SelectedItemsChangedEvent, added, removed,
                selected)));
    }

    private void RemoveLastSelected()
    {
        if (_selected.Count > 0)
            Deselect(_selected[^1]);
    }

    /// <summary>Enter, or Tab in the open list: checks the row the keyboard is on (never unchecks it).</summary>
    private void CheckCurrentItem()
    {
        var row = CurrentItem ?? _filteredItems.FirstOrDefault();
        if (row != null) Select(row.Item);
    }

    // ---- Filtering ----------------------------------------------------------------------------------

    private void ApplyFilter()
    {
        var criteria = _filterText;

        // Sdl's DefaultFilterService: a lower-cased "contains" on ToString().
        IReadOnlyList<MultiSelectComboBoxItem> filtered = string.IsNullOrEmpty(criteria)
            ? _allItems.ToList()
            : _allItems.Where(i => i.Text.ToLower().Contains(criteria.ToLower())).ToList();

        _filteredItems = filtered;
        PseudoClasses.Set(":noresults", filtered.Count == 0);

        if (_list != null)
        {
            _list.ItemsSource = filtered;
            SetCurrentItem(filtered.Count > 0 ? 0 : -1);
        }
    }

    private void SetCurrentItem(int index)
    {
        if (_list == null) return;
        _list.SelectedIndex = index;
        if (index >= 0 && IsDropDownOpen)
            _list.ScrollIntoView(index);
    }

    // ---- Edit mode and focus ------------------------------------------------------------------------

    private void UpdateCanRemoveItems() => CanRemoveItems = IsEditMode && IsEditable;

    private void EnterEditMode()
    {
        IsEditMode = true;
        IsFilterBoxInLayout = true;
        FocusFilterBox();
    }

    private TextBox? FindFilterBox() =>
        _selectedItemsPanel?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == PART_FilterTextBox);

    private void FocusFilterBox()
    {
        // Posted so the box has been enabled and laid out by the time it is focused.
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsEditMode || FindFilterBox() is not { } box) return;
            box.Focus();
            box.CaretIndex = (box.Text ?? string.Empty).Trim().Length;
            box.BringIntoView();
        }, DispatcherPriority.Background);
    }

    /// <summary>Closes the list; optionally clears the filter and leaves edit mode as well.</summary>
    public void CloseDropDown(bool clearFilter, bool leaveEditMode)
    {
        if (clearFilter) FilterText = string.Empty;
        if (leaveEditMode) IsEditMode = false;
        IsDropDownOpen = false;
    }

    /// <summary>Up or Down from the filter box: opens the list and hands it the keyboard.</summary>
    private void OpenAndFocusList()
    {
        if (_list == null || IsWithin(FocusedVisual(), _list)) return;

        IsDropDownOpen = true;
        if (_filteredItems.Count == 0) return;

        if (_list.SelectedIndex < 0) SetCurrentItem(0);
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsDropDownOpen || _list.SelectedIndex < 0) return;
            _list.ScrollIntoView(_list.SelectedIndex);
            _list.ContainerFromIndex(_list.SelectedIndex)?.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Background);
    }

    private Visual? FocusedVisual()
    {
        if (_topLevel?.FocusManager?.GetFocusedElement() is Visual focused) return focused;
        if (_popup?.Child is { } child && TopLevel.GetTopLevel(child)?.FocusManager?.GetFocusedElement() is Visual inPopup)
            return inPopup;
        return null;
    }

    private bool IsFocusWithin()
    {
        var focused = FocusedVisual();
        return focused != null && (IsWithin(focused, this) || IsInPopup(focused));
    }

    private void OnAnyLostFocus(object? sender, RoutedEventArgs e)
    {
        // Sdl only ever closed edit mode for an editable control.
        if (!IsEditable) return;

        DispatcherTimer.RunOnce(() =>
        {
            if (!IsFocusWithin())
                CloseDropDown(clearFilter: true, leaveEditMode: true);
        }, EditModeCloseDelay);
    }

    private static bool IsWithin(Visual? visual, Visual? ancestor) =>
        visual != null && ancestor != null && (visual == ancestor || ancestor.IsVisualAncestorOf(visual));

    private bool IsInPopup(Visual? visual) => _popup?.Child is Visual child && IsWithin(visual, child);

    // ---- Closing on the outside ---------------------------------------------------------------------

    private bool _watchingOutside;

    private void WatchOutside(bool watch)
    {
        if (watch == _watchingOutside) return;
        if (watch && _topLevel == null) return;

        if (watch)
        {
            _topLevel!.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel,
                handledEventsToo: true);
            if (_topLevel is WindowBase window) window.Deactivated += OnWindowDeactivated;
        }
        else if (_topLevel != null)
        {
            _topLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
            if (_topLevel is WindowBase window) window.Deactivated -= OnWindowDeactivated;
        }

        _watchingOutside = watch;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A press anywhere else closes the list and clears the filter, but keeps edit mode.
        var source = e.Source as Visual;
        if (IsWithin(source, this) || IsInPopup(source)) return;
        CloseDropDown(clearFilter: true, leaveEditMode: false);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => IsDropDownOpen = false;

    // ---- Pointer ------------------------------------------------------------------------------------

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is not Visual source || IsInPopup(source)) return;

        if (source.FindAncestorOfType<Button>(includeSelf: true) is { Name: PART_RemoveItemButton } remove)
        {
            if (IsEditMode && IsEditable && remove.DataContext is MultiSelectComboBoxItem chip)
                Deselect(chip.Item);
            EnterEditMode();
            e.Handled = true;
            return;
        }

        if (IsWithin(source, _dropDownButton))
        {
            // The pencil only starts editing; the chevron opens and closes the list.
            if (IsEditMode)
            {
                if (IsDropDownOpen && IsInPopup(FocusedVisual()))
                    CloseDropDown(clearFilter: true, leaveEditMode: false);
                else
                    IsDropDownOpen = !IsDropDownOpen;
            }

            EnterEditMode();
            return;
        }

        if (IsWithin(source, _selectedItemsHost))
        {
            IsDropDownOpen = !IsEditMode || !IsDropDownOpen;
            EnterEditMode();
        }
    }

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Where a shift-click range starts: the row the keyboard was on before this press moved it.
        _rangeAnchor = CurrentItem;
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not
            { DataContext: MultiSelectComboBoxItem row } container) return;

        var toggle = new List<MultiSelectComboBoxItem>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _rangeAnchor != null && _rangeAnchor != row)
        {
            // Shift-click toggles every row strictly between the anchor and the clicked one, as Sdl did,
            // then the clicked one below like any click.
            var from = IndexOf(_rangeAnchor);
            var to = IndexOf(row);
            if (from >= 0 && to >= 0)
            {
                var step = to > from ? 1 : -1;
                for (var i = from + step; i != to; i += step)
                    toggle.Add(_filteredItems[i]);
            }
        }

        toggle.Add(row);
        var add = toggle.Where(r => !_selected.Contains(r.Item)).Select(r => r.Item).ToList();
        var remove = toggle.Where(r => _selected.Contains(r.Item)).Select(r => r.Item).ToList();
        ChangeSelection(add, remove);

        _list!.SelectedItem = row;
        container.Focus();
    }

    private int IndexOf(MultiSelectComboBoxItem row)
    {
        for (var i = 0; i < _filteredItems.Count; i++)
        {
            if (_filteredItems[i] == row) return i;
        }

        return -1;
    }

    // ---- Keyboard -----------------------------------------------------------------------------------

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var source = e.Source as Visual;
        if (IsInPopup(source)) return;

        // Tab in the open list takes the current row and closes it; Shift+Tab just closes it.
        if (e.Key == Key.Tab && IsDropDownOpen)
        {
            if (e.KeyModifiers == KeyModifiers.None)
            {
                CheckCurrentItem();
                CloseDropDown(clearFilter: true, leaveEditMode: false);
                e.Handled = true;
            }
            else if (e.KeyModifiers == KeyModifiers.Shift)
            {
                IsDropDownOpen = false;
                e.Handled = true;
            }

            return;
        }

        if ((e.Key == Key.Up || e.Key == Key.Down) && e.KeyModifiers == KeyModifiers.Alt)
        {
            if (!IsEditMode && IsEditable) EnterEditMode();
            OpenAndFocusList();
            e.Handled = true;
            return;
        }

        if (source is not TextBox { Name: PART_FilterTextBox } box || !IsEditMode) return;

        _backspaceRemoved = false;
        switch (e.Key)
        {
            case Key.Back when box.CaretIndex == 0 && string.IsNullOrWhiteSpace(FilterText):
                if (IsEditable) RemoveLastSelected();
                _backspaceRemoved = true;
                break;
            case Key.Delete:
                if (!string.IsNullOrWhiteSpace(FilterText))
                    FilterText = string.Empty;
                else if (IsEditable)
                    RemoveLastSelected();
                e.Handled = true;
                break;
            case Key.Enter:
                if (IsDropDownOpen)
                {
                    CheckCurrentItem();
                    IsDropDownOpen = false;
                }

                FilterText = string.Empty;
                e.Handled = true;
                break;
            case Key.Escape:
                IsDropDownOpen = false;
                e.Handled = true;
                break;
            case Key.Up:
            case Key.Down:
                OpenAndFocusList();
                e.Handled = true;
                break;
            case Key.C when e.KeyModifiers == KeyModifiers.Control && _selected.Count > 0:
                // Sdl made Ctrl+C in the filter box copy the selection rather than the typed text.
                _ = _topLevel?.Clipboard?.SetTextAsync(SelectedItemsAsText);
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUpBubble(object? sender, KeyEventArgs e)
    {
        var source = e.Source as Visual;
        if (IsInPopup(source)) return;

        if (e.Key == Key.F2 && !IsEditMode)
        {
            EnterEditMode();
            return;
        }

        if (source is not TextBox { Name: PART_FilterTextBox } || !IsEditMode) return;

        switch (e.Key)
        {
            case Key.LeftShift or Key.RightShift or Key.Tab or Key.Delete or Key.Enter or Key.Escape
                or Key.Up or Key.Down or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt:
                return;
            case Key.Back when _backspaceRemoved:
                return;
        }

        // Any other key in the filter box opens the list, as typing did in Sdl.
        IsDropDownOpen = true;
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        var row = CurrentItem;
        if (row == null) return;

        switch (e.Key)
        {
            case Key.Space:
                Toggle(row);
                e.Handled = true;
                break;
            case Key.Enter:
                Select(row.Item);
                CloseDropDown(clearFilter: true, leaveEditMode: false);
                e.Handled = true;
                break;
            case Key.Escape:
                CloseDropDown(clearFilter: true, leaveEditMode: false);
                e.Handled = true;
                break;
            case Key.Tab when e.KeyModifiers == KeyModifiers.None:
                Select(row.Item);
                CloseDropDown(clearFilter: true, leaveEditMode: false);
                e.Handled = true;
                break;
            case Key.Tab when e.KeyModifiers == KeyModifiers.Shift:
                IsDropDownOpen = false;
                e.Handled = true;
                break;
            case Key.Up or Key.Down when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                // Shift+arrow toggles the row being left, and the list then moves on.
                Toggle(row);
                break;
        }
    }
}
