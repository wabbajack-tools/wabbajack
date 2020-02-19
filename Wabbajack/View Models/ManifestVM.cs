using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using CefSharp;
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
                return list.OrderBy(x =>
                {
                    return SortEnum switch
                    {
                        SortBy.Name => x.Name,
                        SortBy.Size => x.Name,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                });
            }

            return list.OrderByDescending(x =>
            {
                return SortEnum switch
                {
                    SortBy.Name => x.Name,
                    SortBy.Size => x.Name,
                    _ => throw new ArgumentOutOfRangeException()
                };
            });
        }

        public ManifestVM(Manifest manifest)
        {
            Manifest = manifest;

            SortByNameCommand = ReactiveCommand.Create(() =>
            {
                if (SortEnum != SortBy.Name)
                    SortEnum = SortBy.Name;
                else
                    SortAscending = !SortAscending;
            });

            SortBySizeCommand = ReactiveCommand.Create(() =>
            {
                if (SortEnum != SortBy.Size)
                    SortEnum = SortBy.Size;
                else
                    SortAscending = !SortAscending;
            });

            _searchResults =
                this.WhenAnyValue(x => x.SearchTerm)
                    /*.CombineLatest(
                        this.WhenAnyValue(x => x.SortAscending),
                        this.WhenAnyValue(x => x.SortEnum), 
                        (term, ascending, sort) => term)*/
                    .Throttle(TimeSpan.FromMilliseconds(800))
                    .Select(term => term?.Trim())
                    .DistinctUntilChanged()
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
