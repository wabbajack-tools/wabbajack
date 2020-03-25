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

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            var files = new HashSet<AbsolutePath>
            {
                _compiler.ModListImage, _compiler.ModListReadme
            };
            if (!files.Any(f => source.AbsolutePath.Equals(f))) return null;
            if (!source.AbsolutePath.Exists) return null;
            var isBanner = source.AbsolutePath == _compiler.ModListImage;
            //var isReadme = source.AbsolutePath == ModListReadme;
            var result = source.EvolveTo<PropertyFile>();
            result.SourceDataID = await _compiler.IncludeFile(source.AbsolutePath.ReadAllBytesAsync());
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

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludePropertyFiles")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludePropertyFiles(compiler);
            }
        }
    }
}
