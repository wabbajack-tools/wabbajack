using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludePropertyFiles : ACompilationStep
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

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludePropertyFiles")]
        public class State : IState
        {
            public ICompilationStep CreateStep(Compiler compiler)
            {
                return new IncludePropertyFiles(compiler);
            }
        }
    }
}