using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ManifestVM : ViewModel
    { 
        public Manifest Manifest { get; set; }

        public string Name => !string.IsNullOrWhiteSpace(Manifest.Name) ? Manifest.Name : "Wabbajack Modlist";
        public string Author => !string.IsNullOrWhiteSpace(Manifest.Author) ? $"Created by {Manifest.Author}" : "Created by Jyggalag";
        public string Description => !string.IsNullOrWhiteSpace(Manifest.Description) ? Manifest.Description : "";
        public string InstallSize => $"Install Size: {Manifest.InstallSize.ToFileSizeString()}";
        public string DownloadSize => $"Download Size: {Manifest.DownloadSize.ToFileSizeString()}";

        public IEnumerable<Archive> Archives => Manifest.Archives;

        [Reactive]
        public string SearchTerm { get; set; }

        private readonly ObservableAsPropertyHelper<IEnumerable<Archive>> _searchResults;
        public IEnumerable<Archive> SearchResults => _searchResults.Value;

        public ManifestVM(Manifest manifest)
        {
            Manifest = manifest;

            _searchResults =
                this.WhenAnyValue(x => x.SearchTerm)
                    .Throttle(TimeSpan.FromMilliseconds(800))
                    .Select(term => term?.Trim())
                    .DistinctUntilChanged()
                    .Select(term =>
                    {
                        return string.IsNullOrWhiteSpace(term)
                            ? Archives
                            : Archives.Where(x => x.Name.StartsWith(term));
                    })
                    .ToGuiProperty(this, nameof(SearchResults), Archives);
        }
    }
}
