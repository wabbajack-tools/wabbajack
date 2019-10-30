using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;

namespace Wabbajack.Lib.CompilationSteps
{
    class IncludePropertyFiles : ACompilationStep
    {
        public IncludePropertyFiles(Compiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            var files = new HashSet<string>
            {
                _compiler.ModListImage, _compiler.ModListReadme
            };
            if (!files.Any(f => source.AbsolutePath.Equals(f))) return null;
            if (!File.Exists(source.AbsolutePath)) return null;
            var isBanner = source.AbsolutePath == _compiler.ModListImage;
            //var isReadme = source.AbsolutePath == ModListReadme;
            var result = source.EvolveTo<PropertyFile>();
            result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            if (isBanner)
            {
                result.Type = PropertyType.Banner;
                _compiler.ModListImage = result.SourceDataID;
            }
            else
            {
                result.Type = PropertyType.Readme;
                _compiler.ModListReadme = result.SourceDataID;
            }
            return result;
        }


    }
}
