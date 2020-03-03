using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ModListVM : ViewModel
    {
        public ModList SourceModList { get; private set; }
        public Exception Error { get; }
        public string ModListPath { get; }
        public string Name => SourceModList?.Name;
        public string Readme => SourceModList?.Readme;
        public string Author => SourceModList?.Author;
        public string Description => SourceModList?.Description;
        public string Website => SourceModList?.Website;
        public ModManager ModManager => SourceModList?.ModManager ?? ModManager.MO2;

        // Image isn't exposed as a direct property, but as an observable.
        // This acts as a caching mechanism, as interested parties will trigger it to be created,
        // and the cached image will automatically be released when the last interested party is gone.
        public IObservable<BitmapImage> ImageObservable { get; }

        public ModListVM(string modListPath)
        {
            ModListPath = modListPath;
            try
            {
                SourceModList = AInstaller.LoadFromFile(modListPath);
            }
            catch (Exception ex)
            {
                Error = ex;
                Utils.Log($"Error while loading the modlist:\n{ex}");
            }

            ImageObservable = Observable.Return(Unit.Default)
                // Download and retrieve bytes on background thread
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(filePath =>
                {
                    try
                    {
                        using (var fs = new FileStream(ModListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
                        {
                            var ms = new MemoryStream();
                            var entry = ar.GetEntry("modlist-image.png");
                            if (entry == null) return default(MemoryStream);
                            using (var e = entry.Open())
                            {
                                e.CopyTo(ms);
                            }
                            return ms;
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.Error(ex, $"Exception while caching Mod List image {Name}");
                        return default(MemoryStream);
                    }
                })
                // Create Bitmap image on GUI thread
                .ObserveOnGuiThread()
                .Select(memStream =>
                {
                    if (memStream == null) return default(BitmapImage);
                    try
                    {
                        return UIUtils.BitmapImageFromStream(memStream);
                    }
                    catch (Exception ex)
                    {
                        Utils.Error(ex, $"Exception while caching Mod List image {Name}");
                        return default(BitmapImage);
                    }
                })
                // If ever would return null, show WJ logo instead
                .Select(x =>
                {
                    return x ?? ResourceLinks.WabbajackLogoNoText.Value;
                })
                .Replay(1)
                .RefCount();
        }

        public void OpenReadmeWindow()
        {
            if (string.IsNullOrEmpty(Readme)) return;
            if (SourceModList.ReadmeIsWebsite)
            {
                Utils.OpenWebsite(Readme);
            }
            else
            {
                using (var fs = new FileStream(ModListPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
                using (var ms = new MemoryStream())
                {
                    var entry = ar.GetEntry(Readme);
                    if (entry == null)
                    {
                        Utils.Log($"Tried to open a non-existent readme: {Readme}");
                        return;
                    }
                    using (var e = entry.Open())
                    {
                        e.CopyTo(ms);
                    }
                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms))
                    {
                        var viewer = new TextViewer(reader.ReadToEnd(), Name);
                        viewer.Show();
                    }
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            // Just drop reference explicitly, as it's large, so it can be GCed
            // Even if someone is holding a stale reference to the VM
            this.SourceModList = null;
        }
    }
}
