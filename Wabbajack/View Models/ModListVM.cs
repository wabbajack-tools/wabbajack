using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ModListVM : ViewModel
    {
        public ModList SourceModList { get; }
        public string ModListPath { get; }
        public string Name => this.SourceModList.Name;
        public string ReportHTML => this.SourceModList.ReportHTML;
        public string Readme => this.SourceModList.Readme;
        public string ImageURL => this.SourceModList.Image;
        public string Author => this.SourceModList.Author;
        public string Description => this.SourceModList.Description;
        public string Website => this.SourceModList.Website;

        // Image isn't exposed as a direct property, but as an observable.
        // This acts as a caching mechanism, as interested parties will trigger it to be created,
        // and the cached image will automatically be released when the last interested party is gone.
        public IObservable<BitmapImage> ImageObservable { get; }

        public ModListVM(ModList sourceModList, string modListPath)
        {
            this.ModListPath = modListPath;
            this.SourceModList = sourceModList;

            this.ImageObservable = Observable.Return(this.ImageURL)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(url =>
                {
                    try
                    {
                        if (!File.Exists(url)) return default(MemoryStream);
                        if (string.IsNullOrWhiteSpace(sourceModList.Image)) return default(MemoryStream);
                        if (sourceModList.Image.Length != 36) return default(MemoryStream);
                        using (var fs = new FileStream(this.ModListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
                        {
                            var ms = new MemoryStream();
                            var entry = ar.GetEntry(sourceModList.Image);
                            using (var e = entry.Open())
                            {
                                e.CopyTo(ms);
                            }
                            return ms;
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.LogToFile($"Exception while caching Mod List image {this.Name}\n{ex.ExceptionToString()}");
                        return default(MemoryStream);
                    }
                })
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(memStream =>
                {
                    try
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = memStream;
                        image.EndInit();
                        image.Freeze();
                        return image;
                    }
                    catch (Exception ex)
                    {
                        Utils.LogToFile($"Exception while caching Mod List image {this.Name}\n{ex.ExceptionToString()}");
                        return default(BitmapImage);
                    }
                })
                .Replay(1)
                .RefCount();
        }
    }
}
