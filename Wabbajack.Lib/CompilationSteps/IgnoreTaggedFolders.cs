using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreTaggedFolders : ACompilationStep
    {
        private readonly IEnumerable<AbsolutePath> _ignoreDirecrtory = new List<AbsolutePath>();
        private readonly string _tag;
        private readonly ACompiler _aCompiler;
        private readonly AbsolutePath _sourcePath;
        private readonly string _reason;

        public IgnoreTaggedFolders(ACompiler compiler, string tag) : base(compiler)
        {
            _aCompiler = (ACompiler)compiler;
            _sourcePath = _aCompiler.SourcePath;
            _tag = tag;
            string rootDirectory = (string)_sourcePath;
            _reason = $"Ignored because folder was tagged with {_tag}";

            _ignoreDirecrtory = Directory.EnumerateFiles(rootDirectory, _tag, SearchOption.AllDirectories).Select(str => (AbsolutePath)str.Replace(_tag, ""));
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            foreach (var folderpath in _ignoreDirecrtory)
            {
                if (!source.AbsolutePath.InFolder(folderpath)) continue;
                var result = source.EvolveTo<IgnoredDirectly>();
                result.Reason = _reason;
                return result;
            }

            return null;
        }
    }
}
