using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public enum SortBy { Name, Size }

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

        [Reactive]
        public bool SortAscending { get; set; } = true;

        [Reactive]
        public SortBy SortEnum { get; set; } = SortBy.Name;

        public ReactiveCommand<Unit, Unit> SortByNameCommand;
        public ReactiveCommand<Unit, Unit> SortBySizeCommand;

        private IEnumerable<Archive> Order(IEnumerable<Archive> list)
        {
            if (SortAscending)
            {
                return SortEnum switch
                {
                    SortBy.Name => list.OrderBy(x => x.Name),
                    SortBy.Size => list.OrderBy(x => x.Size),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            return SortEnum switch
            {
                SortBy.Name => list.OrderByDescending(x => x.Name),
                SortBy.Size => list.OrderByDescending(x => x.Size),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void Swap(SortBy to)
        {
            if (SortEnum != to)
                SortEnum = to;
            else
                SortAscending = !SortAscending;
        }

        public ManifestVM(Manifest manifest)
        {
            Manifest = manifest;

            SortByNameCommand = ReactiveCommand.Create(() => Swap(SortBy.Name));

            SortBySizeCommand = ReactiveCommand.Create(() => Swap(SortBy.Size));

            _searchResults =
                this.WhenAnyValue(x => x.SearchTerm)
                    .CombineLatest(
                        this.WhenAnyValue(x => x.SortAscending),
                        this.WhenAnyValue(x => x.SortEnum), 
                        (term, ascending, sort) => term)
                    .Throttle(TimeSpan.FromMilliseconds(800))
                    .Select(term => term?.Trim())
                    //.DistinctUntilChanged()
                    .Select(term =>
                    {
                        if (string.IsNullOrWhiteSpace(term))
                            return Order(Archives);

                        return Order(Archives.Where(x =>
                        {
                            if (term.StartsWith("hash:"))
                                return x.Hash.StartsWith(term.Replace("hash:", ""));
                            return x.Name.StartsWith(term);
                        }));
                    })
                    .ToGuiProperty(this, nameof(SearchResults), Order(Archives));
        }
    }
}
