using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using ReactiveUI;
using DynamicData;
using DynamicData.Binding;
using Wabbajack.Common;

namespace Wabbajack
{
    public class ModListContentsVM : ViewModel
    {
        private MainWindowVM _mwvm;
        [Reactive]
        public string Name { get; set; }

        [Reactive]
        public ObservableCollection<DetailedStatusItem> Status { get; set; }
        
        [Reactive]
        public string SearchString { get; set; }

        private readonly ReadOnlyObservableCollection<ModListArchive> _archives;
        public ReadOnlyObservableCollection<ModListArchive> Archives => _archives;

        public ModListContentsVM(MainWindowVM mwvm)
        {
            _mwvm = mwvm;
            Status = new ObservableCollectionExtended<DetailedStatusItem>();

            Regex nameMatcher = new Regex(@"(?<=\.)[^\.]+(?=\+State)");
            string TransformClassName(Archive a)
            {
                var cname = a.State.GetType().FullName;
                if (cname == null) return null;

                var match = nameMatcher.Match(cname);
                return match.Success ? match.ToString() : null;
            }

            this.Status
                .ToObservableChangeSet()
                .Transform(a => new ModListArchive
                {
                    Name = a.Name,
                    Size = a.Archive?.Size.ToFileSizeString(),
                    Url = a.Url ?? "",
                    Downloader = TransformClassName(a.Archive) ?? "Unknown",
                    Hash = a.Archive!.Hash.ToBase64()
                })
                .Bind(out _archives)
                .Subscribe()
                .DisposeWith(CompositeDisposable);
        }
    }

    public class ModListArchive
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string Url { get; set; }
        public string Downloader { get; set; }
        public string Hash { get; set; }
    }
}
