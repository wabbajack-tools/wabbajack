using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Compression.BSA;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed.Errors;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DeconstructBSAs : ACompilationStep
    {
        private readonly IEnumerable<RelativePath> _includeDirectly;
        private readonly Func<VirtualFile, List<ICompilationStep>> _microstack;
        private readonly Func<VirtualFile, List<ICompilationStep>> _microstackWithInclude;
        private readonly MO2Compiler _mo2Compiler;

        public DeconstructBSAs(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (MO2Compiler)compiler;
            _includeDirectly = _mo2Compiler.ModInis.Where(kv =>
                {
                    var general = kv.Value.General;
                    if (general.notes != null && (general.notes.Contains(Consts.WABBAJACK_INCLUDE) || general.notes.Contains(Consts.WABBAJACK_NOMATCH_INCLUDE))) return true;
                    if (general.comments != null && (general.comments.Contains(Consts.WABBAJACK_INCLUDE) || general.comments.Contains(Consts.WABBAJACK_NOMATCH_INCLUDE))) return true;
                    return false;
                })
                .Select(kv => kv.Key.RelativeTo(_mo2Compiler.SourcePath))
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
            if (source.Path.RelativeTo(_mo2Compiler.SourcePath).InFolder(_mo2Compiler.SourcePath.Combine(Consts.MO2ModFolderName)))
                if (_includeDirectly.Any(path => source.Path.StartsWith(path)))
                    defaultInclude = true;

            if (source.AbsolutePath.Size >= (long)2 << 31)
            {
                var bsaTest = await BSADispatch.OpenRead(source.AbsolutePath);
                if (bsaTest.State is BSAStateObject)
                {
                    Utils.Error(
                        $"BSA {source.AbsolutePath.FileName} is over 2GB in size, very few programs (Including Wabbajack) can create BSA files this large without causing CTD issues." +
                        $"Please re-compress this BSA into a more manageable size.");
                }
            }

            var sourceFiles = source.File.Children;

            var stack = defaultInclude ? _microstackWithInclude(source.File) : _microstack(source.File);

            var id = Guid.NewGuid().ToString();


            Func<Task>? _cleanup = null;
            if (defaultInclude)
            {
                //_cleanup = await source.File.Context.Stage(source.File.Children);
            }

            var matches = await sourceFiles.PMap(_mo2Compiler.Queue, e => _mo2Compiler.RunStack(stack, new RawSourceFile(e, Consts.BSACreationDir.Combine((RelativePath)id, (RelativePath)e.Name))));


            foreach (var match in matches)
            {
                if (match is IgnoredDirectly)
                    Utils.ErrorThrow(new UnconvertedError($"File required for BSA {source.Path} creation doesn't exist: {match.To}"));
                _mo2Compiler.ExtraFiles.Add(match);
            }

            CreateBSA directive;
            var bsa = await BSADispatch.OpenRead(source.AbsolutePath);
            directive = new CreateBSA(
                state: bsa.State,
                items: bsa.Files.Select(f => f.State).ToList())
            {
                To = source.Path,
                TempID = (RelativePath)id,
            };
            
            if (_cleanup != null)
                await _cleanup();
            return directive;
        }
    }
}
