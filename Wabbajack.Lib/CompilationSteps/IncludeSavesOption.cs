using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeSavesOption: MO2CompilationStep
    {
        private readonly string _tag;

        private readonly AbsolutePath _sourcePath;
        private AbsolutePath[] _profileSavePaths;
        private readonly bool _includeSaves;

        public IncludeSavesOption(ACompiler compiler, string tag) : base(compiler)
        {   
            _tag = tag;
            _sourcePath = compiler.SourcePath;
            string rootDirectory = (string)_sourcePath;
            _includeSaves = File.Exists(((String)Directory.EnumerateFiles(rootDirectory, _tag).ToList().First())) ? true : false;

            _profileSavePaths =
                MO2Compiler.SelectedProfiles.Select(p => MO2Compiler.SourcePath.Combine("profiles", p, "saves")).ToArray();
        }


        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (_includeSaves)
            {
                foreach (var folderpath in _profileSavePaths)
                {
                    if (!source.AbsolutePath.InFolder(folderpath)) continue;
                    var result = source.EvolveTo<InlineFile>();
                    result.SourceDataID = await _compiler.IncludeFile(source.AbsolutePath);
                    return result;
                }
            }
            return null;
        }
    }

}
