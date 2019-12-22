using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Compression.BSA;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed.Errors;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DeconstructBSAs : ACompilationStep
    {
        private readonly IEnumerable<string> _includeDirectly;
        private readonly List<ICompilationStep> _microstack;
        private readonly List<ICompilationStep> _microstackWithInclude;
        private readonly MO2Compiler _mo2Compiler;

        public DeconstructBSAs(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
            _includeDirectly = _mo2Compiler.ModInis.Where(kv =>
                {
                    var general = kv.Value.General;
                    if (general.notes != null && general.notes.Contains(Consts.WABBAJACK_INCLUDE)) return true;
                    if (general.comments != null && general.comments.Contains(Consts.WABBAJACK_INCLUDE)) return true;
                    return false;
                })
                .Select(kv => $"mods\\{kv.Key}\\")
                .ToList();

            _microstack = new List<ICompilationStep>
            {
                new DirectMatch(_mo2Compiler),
                new IncludePatches(_mo2Compiler),
                new DropAll(_mo2Compiler)
            };

            _microstackWithInclude = new List<ICompilationStep>
            {
                new DirectMatch(_mo2Compiler),
                new IncludePatches(_mo2Compiler),
                new IncludeAll(_mo2Compiler)
            };
        }

        public override IState GetState()
        {
            return new State();
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            if (!Consts.SupportedBSAs.Contains(Path.GetExtension(source.Path).ToLower())) return null;

            var defaultInclude = false;
            if (source.Path.StartsWith("mods"))
                if (_includeDirectly.Any(path => source.Path.StartsWith(path)))
                    defaultInclude = true;

            var sourceFiles = source.File.Children;

            var stack = defaultInclude ? _microstackWithInclude : _microstack;

            var id = Guid.NewGuid().ToString();

            var matches = await sourceFiles.PMap(_mo2Compiler.Queue, e => _mo2Compiler.RunStack(stack, new RawSourceFile(e, Path.Combine(Consts.BSACreationDir, id, e.Name))));


            foreach (var match in matches)
            {
                if (match is IgnoredDirectly)
                    Utils.ErrorThrow(new UnconvertedError($"File required for BSA {source.Path} creation doesn't exist: {match.To}"));
                _mo2Compiler.ExtraFiles.Add(match);
            }

            CreateBSA directive;
            using (var bsa = BSADispatch.OpenRead(source.AbsolutePath))
            {
                directive = new CreateBSA
                {
                    To = source.Path,
                    TempID = id,
                    State = bsa.State,
                    FileStates = bsa.Files.Select(f => f.State).ToList()
                };
            }

            return directive;
        }

        [JsonObject("DeconstructBSAs")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new DeconstructBSAs(compiler);
            }
        }
    }
}
