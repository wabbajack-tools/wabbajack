using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludePatches : ACompilationStep
    {
        private readonly Dictionary<string, IGrouping<string, VirtualFile>> _indexed;

        public IncludePatches(ACompiler compiler) : base(compiler)
        {
            _indexed = _compiler.IndexedFiles.Values
                .SelectMany(f => f)
                .GroupBy(f => Path.GetFileName(f.Name).ToLower())
                .ToDictionary(f => f.Key);
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!_indexed.TryGetValue(Path.GetFileName(source.File.Name.ToLower()), out var value))
                return null;

            var found = value.OrderByDescending(f => (f.FilesInFullPath.First() ?? f).LastModified).First();

            var e = source.EvolveTo<PatchedFromArchive>();
            e.ArchiveHashPath = found.MakeRelativePaths();
            e.To = source.Path;
            e.Hash = source.File.Hash;

            Utils.TryGetPatch(found.Hash, source.File.Hash, out var data);

            if (data != null)
                e.PatchID = _compiler.IncludeFile(data);

            return e;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludePatches")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludePatches(compiler);
            }
        }
    }
}