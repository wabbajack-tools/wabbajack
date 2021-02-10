using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeTaggedFolders : ACompilationStep
    {
        private readonly IEnumerable<AbsolutePath> _includeDirectly = new List<AbsolutePath>();
        private readonly string _tag;
        private readonly NativeCompiler _nativeCompiler;
        private readonly AbsolutePath _sourcePath;

        public IncludeTaggedFolders(ACompiler compiler, string tag) : base(compiler)
        {   
            _nativeCompiler = (NativeCompiler)compiler;
            _sourcePath = _nativeCompiler.SourcePath;
            _tag = tag;
            string rootDirectory = (string)_sourcePath;

            _includeDirectly = Directory.EnumerateFiles(rootDirectory, _tag, SearchOption.AllDirectories).Select(str => (AbsolutePath)str.Replace(_tag, ""));
        }


        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            foreach (var folderpath in _includeDirectly)
            {
                Utils.Log($"IncludeTaggedFolders Taggedfolder: {folderpath}");
                if (!source.AbsolutePath.InFolder(folderpath)) continue;
                var result = source.EvolveTo<InlineFile>();
                result.SourceDataID = await _compiler.IncludeFile(source.AbsolutePath);
                return result;
            }

            return null;
        }
    }

}
