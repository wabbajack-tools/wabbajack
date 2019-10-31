using System;
using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Compression.BSA;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DeconstructBSAs : ACompilationStep
    {
        private readonly IEnumerable<string> _include_directly;
        private readonly List<ICompilationStep> _microstack;
        private readonly List<ICompilationStep> _microstackWithInclude;

        public DeconstructBSAs(Compiler compiler) : base(compiler)
        {
            _include_directly = _compiler.ModInis.Where(kv =>
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
                new DirectMatch(_compiler),
                new IncludePatches(_compiler),
                new DropAll(_compiler)
            };

            _microstackWithInclude = new List<ICompilationStep>
            {
                new DirectMatch(_compiler),
                new IncludePatches(_compiler),
                new IncludeAll(_compiler)
            };
        }

        public override IState GetState()
        {
            return new State();
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!Consts.SupportedBSAs.Contains(Path.GetExtension(source.Path).ToLower())) return null;

            var defaultInclude = false;
            if (source.Path.StartsWith("mods"))
                if (_include_directly.Any(path => source.Path.StartsWith(path)))
                    defaultInclude = true;

            var source_files = source.File.FileInArchive;

            var stack = defaultInclude ? _microstackWithInclude : _microstack;

            var id = Guid.NewGuid().ToString();

            var matches = source_files.PMap(e => Compiler.RunStack(stack, new RawSourceFile(e)
            {
                Path = Path.Combine(Consts.BSACreationDir, id, e.Paths.Last())
            }));


            foreach (var match in matches)
            {
                if (match is IgnoredDirectly)
                    Utils.Error($"File required for BSA {source.Path} creation doesn't exist: {match.To}");
                _compiler.ExtraFiles.Add(match);
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
            public ICompilationStep CreateStep(Compiler compiler)
            {
                return new DeconstructBSAs(compiler);
            }
        }
    }
}