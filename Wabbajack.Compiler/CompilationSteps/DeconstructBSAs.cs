using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Compression.BSA;
using Wabbajack.DTOs;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.VFS;

namespace Wabbajack.Compiler.CompilationSteps;

public class DeconstructBSAs : ACompilationStep
{
    private readonly IEnumerable<RelativePath> _includeDirectly;
    private readonly Func<VirtualFile, List<ICompilationStep>> _microstack;
    private readonly Func<VirtualFile, List<ICompilationStep>> _microstackWithInclude;
    private readonly MO2Compiler _mo2Compiler;

    public DeconstructBSAs(ACompiler compiler) : base(compiler)
    {
        _mo2Compiler = (MO2Compiler) compiler;
        _includeDirectly = _mo2Compiler.ModInis.Where(kv =>
            {
                var general = kv.Value["General"];
                if (general["notes"] != null && (general["notes"].Contains(Consts.WABBAJACK_INCLUDE) ||
                                                 general["notes"].Contains(Consts.WABBAJACK_NOMATCH_INCLUDE)))
                    return true;
                if (general["comments"] != null && (general["comments"].Contains(Consts.WABBAJACK_INCLUDE) ||
                                                    general["comments"].Contains(Consts.WABBAJACK_NOMATCH_INCLUDE)))
                    return true;
                return false;
            })
            .Select(kv => kv.Key.RelativeTo(_mo2Compiler._settings.Source))
            .ToList();

        _microstack = bsa => new List<ICompilationStep>
        {
            new DirectMatch(_mo2Compiler),
            new MatchSimilarTextures(_mo2Compiler),
            new IncludePatches(_mo2Compiler, bsa),
            new DropAll(_mo2Compiler)
        };

        _microstackWithInclude = bsa => new List<ICompilationStep>
        {
            new DirectMatch(_mo2Compiler),
            new MatchSimilarTextures(_mo2Compiler),
            new IncludePatches(_mo2Compiler, bsa),
            new IncludeAll(_mo2Compiler)
        };
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!Consts.SupportedBSAs.Contains(source.Path.Extension)) return null;

        var defaultInclude = false;
        if (source.Path.RelativeTo(_mo2Compiler._settings.Source)
            .InFolder(_mo2Compiler._settings.Source.Combine(Consts.MO2ModFolderName)))
            if (_includeDirectly.Any(path => source.Path.InFolder(path)))
                defaultInclude = true;

        if (source.AbsolutePath.Size() >= (long) 2 << 31)
        {
            var bsaTest = await BSADispatch.Open(source.AbsolutePath);
            if (bsaTest.State is BSAState)
                throw new CompilerException(
                    $"BSA {source.AbsolutePath.FileName} is over 2GB in size, very few programs (Including Wabbajack) can create BSA files this large without causing CTD issues." +
                    "Please re-compress this BSA into a more manageable size.");
        }

        var sourceFiles = source.File.Children;

        var stack = defaultInclude ? _microstackWithInclude(source.File) : _microstack(source.File);

        var id = Guid.NewGuid().ToString().ToRelativePath();


        Func<Task>? _cleanup = null;
        if (defaultInclude)
        {
            //_cleanup = await source.File.Context.Stage(source.File.Children);
        }

        var matches = await sourceFiles.PMapAll(_compiler.CompilerLimiter,
                e => _mo2Compiler.RunStack(stack,
                    new RawSourceFile(e, Consts.BSACreationDir.Combine(id, (RelativePath) e.Name))))
            .ToList();


        foreach (var match in matches)
        {
            if (match is IgnoredDirectly ignored)
                throw new CompilerException($"File required for BSA {source.Path} creation doesn't exist: {match.To} reason {ignored.Reason}");

            _mo2Compiler.ExtraFiles.Add(match);
        }

        var bsa = await BSADispatch.Open(source.AbsolutePath);
        var directive = new CreateBSA
        {
            State = bsa.State,
            FileStates = bsa.Files.Select(f => f.State).ToArray(),
            To = source.Path,
            Hash = source.Hash,
            TempID = (RelativePath) id
        };

        if (_cleanup != null)
            await _cleanup();
        return directive;
    }
}