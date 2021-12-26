using ReactiveUI;
using System;
using System.IO;
using System.IO.Compression;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack
{
    public class ModListVM : ViewModel
    {
        public ModList SourceModList { get; private set; }
        public ModlistMetadata SourceModListMetadata { get; private set; }
        public Exception Error { get; }
        public AbsolutePath ModListPath { get; }
        public string Name => SourceModList?.Name;
        public string Readme => SourceModList?.Readme;
        public string Author => SourceModList?.Author;
        public string Description => SourceModList?.Description;
        public Uri Website => SourceModList?.Website;
        public ModManager ModManager => SourceModList?.ModManager ?? ModManager.MO2;
        public Version Version => SourceModList?.Version;
        public Version WabbajackVersion => SourceModList?.WabbajackVersion;
        public bool IsNSFW => SourceModList?.IsNSFW ?? false;

        // Image isn't exposed as a direct property, but as an observable.
        // This acts as a caching mechanism, as interested parties will trigger it to be created,
        // and the cached image will automatically be released when the last interested party is gone.
        public IObservable<BitmapImage> ImageObservable { get; }

        public ModListVM(AbsolutePath modListPath)
        {
            ModListPath = modListPath;
            try
            {
                SourceModList = AInstaller.LoadFromFile(modListPath);
                var metadataPath = modListPath.WithExtension(Consts.ModlistMetadataExtension);
                if (metadataPath.Exists)
                {
                    try
                    {
                        SourceModListMetadata = metadataPath.FromJson<ModlistMetadata>();
                    }
                    catch (Exception)
                    {
                        SourceModListMetadata = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Error = ex;
                Utils.Error(ex, "Exception while loading the modlist!");
            }

            ImageObservable = Observable.Return(Unit.Default)
                // Download and retrieve bytes on background thread
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectAsync(async filePath =>
                {
                    try
                    {
                        await using var fs = await ModListPath.OpenShared();
                        using var ar = new ZipArchive(fs, ZipArchiveMode.Read);
                        var ms = new MemoryStream();
                        var entry = ar.GetEntry("modlist-image.png");
                        if (entry == null) return default(MemoryStream);
                        await using var e = entry.Open();
                        e.CopyTo(ms);
                        return ms;
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
                .Select(x => x ?? ResourceLinks.WabbajackLogoNoText.Value)
                .Replay(1)
                .RefCount();
        }

        public void OpenReadme()
        {
            if (string.IsNullOrEmpty(Readme)) return;
            Utils.OpenWebsite(new Uri(Readme));
        }

        public override void Dispose()
        {
            base.Dispose();
            // Just drop reference explicitly, as it's large, so it can be GCed
            // Even if someone is holding a stale reference to the VM
            SourceModList = null;
        }
    }
}
