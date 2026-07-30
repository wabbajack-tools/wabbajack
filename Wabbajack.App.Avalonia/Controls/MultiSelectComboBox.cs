using System;
using System.Collections;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Wabbajack;

// Minimal Avalonia replacement for the SDL WPF Toolkit MultiSelectComboBox, preserving the
// ItemsSource / SelectedItems / SelectedItemsChanged surface the gallery mod/tag filters use.
// TODO(avalonia-control): give this a real multi-select dropdown template/interaction.
public class MultiSelectComboBox : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, IEnumerable?>(nameof(ItemsSource));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly StyledProperty<SelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, SelectionMode>(nameof(SelectionMode), SelectionMode.Multiple);

    public SelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, bool>(nameof(IsEditable));

    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public IList SelectedItems { get; } = new ObservableCollection<object>();

    public event EventHandler? SelectedItemsChanged;

    protected void RaiseSelectedItemsChanged() => SelectedItemsChanged?.Invoke(this, EventArgs.Empty);
}
