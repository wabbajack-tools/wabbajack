using ReactiveUI;
using System;
using System.IO;
using System.IO.Compression;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Consts = Wabbajack.Consts;

namespace Wabbajack
{
    public class ModListVM : ViewModel
    {
        private readonly DTOSerializer _dtos;
        private readonly ILogger<ModListVM> _logger;
        public ModList SourceModList { get; private set; }
        public ModlistMetadata SourceModListMetadata { get; private set; }
        
        [Reactive]
        public Exception Error { get; set; }
        public AbsolutePath ModListPath { get; }
        public string Name => SourceModList?.Name;
        public string Readme => SourceModList?.Readme;
        public string Author => SourceModList?.Author;
        public string Description => SourceModList?.Description;
        public Uri Website => SourceModList?.Website;
        public Version Version => SourceModList?.Version;
        public Version WabbajackVersion => SourceModList?.WabbajackVersion;
        public bool IsNSFW => SourceModList?.IsNSFW ?? false;

        // Image isn't exposed as a direct property, but as an observable.
        // This acts as a caching mechanism, as interested parties will trigger it to be created,
        // and the cached image will automatically be released when the last interested party is gone.
        public IObservable<BitmapImage> ImageObservable { get; }

        public ModListVM(ILogger<ModListVM> logger, AbsolutePath modListPath, DTOSerializer dtos)
        {
            _dtos = dtos;
            _logger = logger;
            
            ModListPath = modListPath;

            Task.Run(async () =>
            {
                try
                {
                    SourceModList = await StandardInstaller.LoadFromFile(_dtos, modListPath);
                    var metadataPath = modListPath.WithExtension(Ext.ModlistMetadataExtension);
                    if (metadataPath.FileExists())
                    {
                        try
                        {
                            SourceModListMetadata = await metadataPath.FromJson<ModlistMetadata>();
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
                    _logger.LogError(ex, "Exception while loading the modlist!");
                }
            });

            ImageObservable = Observable.Return(Unit.Default)
                // Download and retrieve bytes on background thread
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectAsync(async filePath =>
                {
                    try
                    {
                        await using var fs = ModListPath.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
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
                        _logger.LogError(ex, "Exception while caching Mod List image {Name}", Name);
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
                        _logger.LogError(ex, "Exception while caching Mod List image {Name}", Name);
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
            UIUtils.OpenWebsite(new Uri(Readme));
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
