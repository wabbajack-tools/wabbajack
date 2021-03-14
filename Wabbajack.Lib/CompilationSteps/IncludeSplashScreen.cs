using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeSplashScreen : MO2CompilationStep
    {
        private readonly string _splash;

        private readonly AbsolutePath _sourcePath;
        private List<AbsolutePath> _splashPath;
        private readonly bool _splashExists;

        public IncludeSplashScreen(ACompiler compiler) : base(compiler)
        {
            _splash = "splash.png";
            _sourcePath = compiler.SourcePath;
            string rootDirectory = (string)_sourcePath;
            _splashExists = File.Exists(((String)Directory.EnumerateFiles(rootDirectory, _splash).ToList().First())) ? true : false;

            _splashPath = Directory.EnumerateFiles(rootDirectory, _splash).Select(str => (AbsolutePath)str)
                .ToList();
        }


        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (_splashExists)
            {
                foreach (var folderpath in _splashPath)
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
