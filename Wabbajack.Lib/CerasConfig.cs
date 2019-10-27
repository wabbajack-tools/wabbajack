using Ceras;
using Compression.BSA;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Lib
{
    public class CerasConfig
    {
        public static SerializerConfig Config = new SerializerConfig()
        {
            KnownTypes =
            {
                typeof(ModList), typeof(Game), typeof(Directive), typeof(IgnoredDirectly),
                typeof(NoMatch), typeof(InlineFile), typeof(PropertyType), typeof(CleanedESM),
                typeof(RemappedInlineFile), typeof(FromArchive), typeof(CreateBSA), typeof(PatchedFromArchive),
                typeof(SourcePatch), typeof(MergedPatch), typeof(Archive), typeof(IndexedArchive), typeof(IndexedEntry),
                typeof(IndexedArchiveEntry), typeof(BSAIndexedEntry), typeof(VirtualFile), 
                typeof(ArchiveStateObject), typeof(FileStateObject), typeof(IDownloader), 
                typeof(IUrlDownloader), typeof(AbstractDownloadState), typeof(ManualDownloader),
                typeof(DropboxDownloader), typeof(GoogleDriveDownloader), typeof(HTTPDownloader),
                typeof(MegaDownloader), typeof(ModDBDownloader), typeof(NexusDownloader)
            }
        };
    }
}
