using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeTaggedFiles : ACompilationStep
    {
        private List<AbsolutePath> _includeDirectly = new List<AbsolutePath>();
        private List<AbsolutePath> _tagFiles;
        private readonly string _tag;
        private readonly ACompiler _aCompiler;
        private readonly AbsolutePath _sourcePath;

        public IncludeTaggedFiles(ACompiler compiler, string tag) : base(compiler)
        {   
            _aCompiler = (ACompiler)compiler;
            _sourcePath = _aCompiler.SourcePath;
            _tag = tag;
            string rootDirectory = (string)_sourcePath;

            _tagFiles = Directory.EnumerateFiles(rootDirectory, _tag, SearchOption.AllDirectories)
                .Select(str => (AbsolutePath)str)
                .ToList();

            foreach (var tagFile in _tagFiles)
            {
                _includeDirectly.Add(tagFile);
                string[] taggedFiles = File.ReadAllLines((String)tagFile);
                foreach (var taggedFile in taggedFiles)
                {
                    _includeDirectly.Add((AbsolutePath)(tagFile.ToString()
                        .Replace(_tag,taggedFile)));
                }
            }

        }


        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            foreach (var folderpath in _includeDirectly)
            {
                if (!source.AbsolutePath.Equals(folderpath) && !source.AbsolutePath.InFolder(folderpath)) continue;
                var result = source.EvolveTo<InlineFile>();
                result.SourceDataID = await _compiler.IncludeFile(source.AbsolutePath);
                return result;
            }

            return null;
        }
    }

}
