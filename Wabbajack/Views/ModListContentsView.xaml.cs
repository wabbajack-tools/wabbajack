using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Xps;
using ReactiveUI;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI.Fody.Helpers;

namespace Wabbajack
{
    public partial class ModListContentsView
    {

        public ModListContentsView()
        {
            InitializeComponent();
            this.WhenActivated(disposable =>
            {
                /*
                var queryText = this
                    .WhenAny(x => x.SearchBox)
                    .Select(t => t.Text)
                    .StartWith("")
                    .Select<string, Func<ModListArchive, bool>>(s => (ModListArchive ar) => 
                        string.IsNullOrEmpty(s) ||
                        ar.Name.ContainsCaseInsensitive(s) ||
                        ar.Downloader.ContainsCaseInsensitive(s) ||
                        ar.Hash.ContainsCaseInsensitive(s) ||
                        ar.Size.ContainsCaseInsensitive(s) ||
                        ar.Url.ContainsCaseInsensitive(s));
                        */
                this.ArchiveGrid.ItemsSource = this.ViewModel.Archives;
                
                this.WhenAny(x => x.ViewModel.Name)
                    .BindToStrict(this, x => x.ModListTitle.Title)
                    .DisposeWith(disposable);
                /*this.WhenAny(x => x.ViewModel.Archives)
                    .BindToStrict(this, x => x.ArchiveGrid.ItemsSource)
                    .DisposeWith(disposable);*/
            });
        }
    }
}

