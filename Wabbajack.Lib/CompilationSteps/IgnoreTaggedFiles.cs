using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreTaggedFiles : ACompilationStep
    {
        private List<AbsolutePath> _ignoreList = new List<AbsolutePath>();
        private List<AbsolutePath> _tagFiles;
        private readonly string _tag;
        private readonly ACompiler _aCompiler;
        private readonly AbsolutePath _sourcePath;
        private readonly string _reason;

        public IgnoreTaggedFiles(ACompiler compiler, string tag) : base(compiler)
        {
            _aCompiler = (ACompiler)compiler;
            _sourcePath = _aCompiler.SourcePath;
            _tag = tag;
            string rootDirectory = (string)_sourcePath;

            _reason = $"Ignored because folder/file was tagged with {_tag}";

            _tagFiles = Directory.EnumerateFiles(rootDirectory, _tag, SearchOption.AllDirectories)
                .Select(str => (AbsolutePath)str)
                .ToList();

            foreach (var tagFile in _tagFiles)
            {
                _ignoreList.Add(tagFile);
                string[] taggedFiles = File.ReadAllLines((String)tagFile);
                foreach (var taggedFile in taggedFiles)
                {
                    _ignoreList.Add((AbsolutePath)(tagFile.ToString()
                        .Replace(_tag,taggedFile)));
                }
            }

        }


        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            foreach (var folderpath in _ignoreList)
            {
                if (!source.AbsolutePath.Equals(folderpath) && !source.AbsolutePath.InFolder(folderpath)) continue;
                var result = source.EvolveTo<IgnoredDirectly>();
                result.Reason = _reason;
                return result;
            }

            return null;
        }
    }

}
