using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using DynamicData;
using DynamicData.Binding;

namespace Wabbajack
{
    public class ModListContentsVM : BackNavigatingVM
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

        private static readonly Regex NameMatcher = new(@"(?<=\.)[^\.]+(?=\+State)", RegexOptions.Compiled); 
        public ModListContentsVM(MainWindowVM mwvm) : base(mwvm)
        {
            _mwvm = mwvm;
            Status = new ObservableCollectionExtended<DetailedStatusItem>();
            
            string TransformClassName(Archive a)
            {
                var cname = a.State.GetType().FullName;
                if (cname == null) return null;

                var match = NameMatcher.Match(cname);
                return match.Success ? match.ToString() : null;
            }

            this.Status
                .ToObservableChangeSet()
                .Transform(a => new ModListArchive
                {
                    Name = a.Name,
                    Size = a.Archive?.Size ?? 0,
                    Url = a.Url ?? "",
                    Downloader = TransformClassName(a.Archive) ?? "Unknown",
                    Hash = a.Archive!.Hash.ToBase64()
                })
                .Filter(this.WhenAny(x => x.SearchString)
                    .StartWith("")
                    .Throttle(TimeSpan.FromMilliseconds(250))
                    .Select<string, Func<ModListArchive, bool>>(s => (ModListArchive ar) => 
                        string.IsNullOrEmpty(s) ||
                        ar.Name.ContainsCaseInsensitive(s) ||
                        ar.Downloader.ContainsCaseInsensitive(s) ||
                        ar.Hash.ContainsCaseInsensitive(s) ||
                        ar.Size.ToString() == s ||
                        ar.Url.ContainsCaseInsensitive(s)))
                .ObserveOnGuiThread()
                .Bind(out _archives)
                .Subscribe()
                .DisposeWith(CompositeDisposable);
        }
    }

    public class ModListArchive
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string Url { get; set; }
        public string Downloader { get; set; }
        public string Hash { get; set; }
    }
}
