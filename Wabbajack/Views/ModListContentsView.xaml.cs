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

                this.ArchiveGrid.ItemsSource = this.ViewModel.Archives;
                
                this.WhenAny(x => x.ViewModel.Name)
                    .BindToStrict(this, x => x.ModListTitle.Title)
                    .DisposeWith(disposable);
                this.BindStrict(ViewModel, x => x.SearchString, x => x.SearchBox.Text)
                    .DisposeWith(disposable);
                this.WhenAny(x => x.ViewModel.BackCommand)
                    .BindToStrict(this, x => x.BackButton.Command)
                    .DisposeWith(disposable);
            });
        }
    }
}

