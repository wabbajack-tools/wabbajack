using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludePropertyFiles : ACompilationStep
    {

        public IncludePropertyFiles(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            var files = new HashSet<AbsolutePath>
            {
                _compiler.ModListImage
            };
            if (!files.Any(f => source.AbsolutePath.Equals(f))) return null;
            if (!source.AbsolutePath.Exists) return null;
            var isBanner = source.AbsolutePath == _compiler.ModListImage;
            //var isReadme = source.AbsolutePath == ModListReadme;
            var result = source.EvolveTo<PropertyFile>();
            result.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
            if (isBanner)
            {
                result.Type = PropertyType.Banner;
                _compiler.ModListImage = result.SourceDataID.RelativeTo(_compiler.ModListOutputFolder);
            }

            return result;
        }
    }
}
