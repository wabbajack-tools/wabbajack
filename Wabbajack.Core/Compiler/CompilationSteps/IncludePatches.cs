using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using F23.StringSimilarity;
using IniParser.Model;
using Wabbajack.Common;
using Wabbajack.Compiler.PatchCache;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;
using Wabbajack.VFS;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludePatches : ACompilationStep
{
    private readonly Dictionary<RelativePath, IGrouping<RelativePath, VirtualFile>> _indexed;
    private readonly VirtualFile? _bsa;
    private readonly Dictionary<RelativePath, IEnumerable<VirtualFile>> _indexedByName;
    private readonly bool _isGenericGame;

    public IncludePatches(ACompiler compiler, VirtualFile? constructingFromBSA = null) : base(compiler)
    {
        _bsa = constructingFromBSA;
        _compiler = compiler;
        _indexed = _compiler.IndexedFiles.Values
            .SelectMany(f => f)
            .GroupBy(f => f.Name.FileName)
            .ToDictionary(f => f.Key);
        _indexedByName = _indexed.Values
            .SelectMany(s => s)
            .Where(f => f.IsNative)
            .GroupBy(f => f.Name.FileName)
            .ToDictionary(f => f.Key, f => (IEnumerable<VirtualFile>) f);

        _isGenericGame = _compiler._settings.Game.MetaData().IsGenericMO2Plugin;
    }

    private IncludePatches(ACompiler compiler, 
        VirtualFile bsa,
        Dictionary<RelativePath, IEnumerable<VirtualFile>> indexedByName,
        Dictionary<RelativePath, IGrouping<RelativePath, VirtualFile>> indexed) : base(compiler)
    {
        _bsa = bsa;
        _indexedByName = indexedByName;
        _indexed = indexed;
    }

    public IncludePatches WithBSA(VirtualFile constructingFromBSA)
    {
        return new IncludePatches(_compiler, constructingFromBSA, _indexedByName, _indexed);
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (_isGenericGame)
            if (source.Path.InFolder(Consts.GameFolderFilesDir))
                return null;

        var name = source.File.Name.FileName;
        var nameWithoutExt = name;
        if (name.Extension == Ext.Mohidden)
            nameWithoutExt = name.WithoutExtension();

        if (!_indexed.TryGetValue(name, out var choices))
            _indexed.TryGetValue(nameWithoutExt, out choices);

        IniData? modIni = null;

        if (_compiler is MO2Compiler)
        {
            if (_bsa == null && source.File.IsNative &&
                source.AbsolutePath.InFolder(((MO2Compiler) _compiler).MO2ModsFolder))
            {
                ((MO2Compiler) _compiler).ModInis.TryGetValue(ModForFile(source.AbsolutePath), out modIni);
            }
            else if (_bsa != null)
            {
                var bsaPath = _bsa.FullPath.Base;
                var modPath = ModForFile(bsaPath);
                if (modPath != default) 
                    ((MO2Compiler) _compiler).ModInis.TryGetValue(modPath, out modIni);
            }
        }

        var installationFile = modIni?["General"]["installationFile"];

        VirtualFile[] found = { };

        // Find based on exact file name + ext
        if (choices != null && installationFile != null)
        {
            var relName = (RelativePath) Path.GetFileName(installationFile);
            found = choices.Where(f => f.FilesInFullPath.First().Name.FileName == relName).ToArray();
        }

        // Find based on file name only (not ext)
        if (found.Length == 0 && choices != null) found = choices.ToArray();

        // Find based on matchAll=<archivename> in [General] in meta.ini
        var matchAllName = modIni?["General"]?["matchAll"];
        if (matchAllName != null && found.Length == 0)
        {
            var relName = (RelativePath) Path.GetFileName(matchAllName);
            if (_indexedByName.TryGetValue(relName, out var arch))
            {
                var dist = new Levenshtein();
                found = arch.SelectMany(a => a.ThisAndAllChildren)
                    .OrderBy(a => dist.Distance(a.Name.FileName.ToString(), source.File.Name.FileName.ToString()))
                    .Take(3)
                    .ToArray();
            }
        }

        if (found.Length == 0)
            return null;


        var e = source.EvolveTo<PatchedFromArchive>();

        var patches = await found
            .SelectAsync(async c => (await _compiler._patchCache.GetPatch(c.Hash, source.File.Hash), c))
            .ToList();

        if (patches.All(p => p.Item1 != null))
        {
            var (patch, file) = PickPatch(_compiler, patches);
            e.FromHash = file.Hash;
            e.ArchiveHashPath = file.MakeRelativePaths();
            e.PatchID = await _compiler.IncludeFile(await _compiler._patchCache.GetData(patch));
        }
        else
        {
            _compiler._patchOptions[e] = found;
        }

        return e;
    }

    public static (CacheEntry, VirtualFile) PickPatch(ACompiler compiler,
        IEnumerable<(CacheEntry? data, VirtualFile file)> patches)
    {
        var ordered = patches
            .Select(f => (f.data!, f.file))
            .OrderBy(f => f.Item1.PatchSize)
            .ToArray();

        var primaryChoice = ordered.FirstOrDefault(itm =>
        {
            var baseHash = itm.file.TopParent.Hash;

            // If this file doesn't come from a game use it
            if (!compiler.GamesWithHashes.TryGetValue(baseHash, out var games))
                return true;

            // Otherwise skip files that are not from the primary game
            return games.Contains(compiler._settings.Game);
        });

        // If we didn't find a file from an archive or the primary game, use a secondary game file.
        var result = primaryChoice != default ? primaryChoice : ordered.FirstOrDefault();
        return result;
    }

    private AbsolutePath ModForFile(AbsolutePath file)
    {
        if (!file.InFolder(((MO2Compiler) _compiler).MO2ModsFolder))
            return default;
        return file.RelativeTo(((MO2Compiler) _compiler).MO2ModsFolder).TopParent
            .RelativeTo(((MO2Compiler) _compiler).MO2ModsFolder);
    }
}