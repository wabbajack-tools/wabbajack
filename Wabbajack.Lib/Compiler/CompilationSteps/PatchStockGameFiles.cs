

using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.VFS;

namespace Wabbajack.Compiler.CompilationSteps;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Hashing.xxHash64;
using System.Linq;
using System.Threading.Tasks;
using F23.StringSimilarity;


public class PatchStockGameFiles : ACompilationStep
{
    private readonly Task<ILookup<Hash, Archive>> _files;
    private readonly Levenshtein _distFn;
    private readonly Client _client;

    public PatchStockGameFiles(ACompiler compiler, Client client) : base(compiler)
    {
        _client = client;
        _distFn = new Levenshtein();
        _files = Task.Run(async () => (await _client.GetGameArchives(Game.SkyrimSpecialEdition, "1.5.97.0"))
            .ToLookup(f => f.Hash));
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (_compiler._settings.Game != Game.SkyrimSpecialEdition) return null;

        var found = (await _files)[source.Hash];
        if (!found.Any()) return null;

        var srcFile = _compiler.IndexedArchives
            .Where(f => f.State is GameFileSource)
            .MinBy(l => _distFn.Distance(l.File.Name.FileName.ToString(), source.Path.FileName.ToString()));

        if (srcFile == null) return null;
        
        var e = source.EvolveTo<PatchedFromArchive>();

        var data = await _compiler._patchCache.GetPatch(srcFile.File.Hash, source.File.Hash);

        if (data != null)
        {
            e.FromHash = srcFile.File.Hash;
            e.ArchiveHashPath = srcFile.File.MakeRelativePaths();
            e.PatchID = await _compiler.IncludeFile(await _compiler._patchCache.GetData(data));
        }
        else
        {
            _compiler._patchOptions[e] = new[]{srcFile.File};
        }

        return e;
    }
}