using System;
using System.Collections;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

public partial class ModListGalleryView : ReactiveUserControl<ModListGalleryVM>
{
    // Guards against re-entrancy: when we push selection from the VM into the ListBox
    // (init / reset), the ListBox raises SelectionChanged which would otherwise write
    // straight back into the VM. Only user-driven changes should sync ListBox -> VM.
    private bool _syncingTags;
    private bool _syncingMods;

    public ModListGalleryView()
    {
        AvaloniaXamlLoader.Load(this);

        var tagsListBox = this.FindControl<ListBox>("TagsListBox")!;
        var modsListBox = this.FindControl<ListBox>("ModsListBox")!;

        tagsListBox.SelectionChanged += (_, _) =>
        {
            if (_syncingTags) return;
            var vm = ViewModel;
            if (vm?.HasTags is null) return;

            vm.HasTags.Clear();
            foreach (var item in tagsListBox.SelectedItems!.OfType<ModListTag>())
                vm.HasTags.Add(item);
        };

        modsListBox.SelectionChanged += (_, _) =>
        {
            if (_syncingMods) return;
            var vm = ViewModel;
            if (vm?.HasMods is null) return;

            vm.HasMods.Clear();
            foreach (var item in modsListBox.SelectedItems!.OfType<ModListMod>())
                vm.HasMods.Add(item);
        };

        this.WhenActivated(disposables =>
        {
            // VM -> ListBox sync. Fires on attach and whenever the VM replaces the
            // collection (e.g. ResetFilters assigns a fresh empty collection), keeping
            // the ListBox selection in step. Guarded so it doesn't echo back into the VM.
            this.WhenAnyValue(x => x.ViewModel!.HasTags)
                .Subscribe(tags =>
                {
                    _syncingTags = true;
                    try { SyncListBoxFromVm(tagsListBox, tags); }
                    finally { _syncingTags = false; }
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel!.HasMods)
                .Subscribe(mods =>
                {
                    _syncingMods = true;
                    try { SyncListBoxFromVm(modsListBox, mods); }
                    finally { _syncingMods = false; }
                })
                .DisposeWith(disposables);
        });
    }

    private static void SyncListBoxFromVm(ListBox listBox, IEnumerable? selected)
    {
        listBox.SelectedItems!.Clear();
        if (selected is null) return;
        foreach (var item in selected)
            listBox.SelectedItems.Add(item);
    }
}
