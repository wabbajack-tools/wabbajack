using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludePropertyFiles : ACompilationStep
    {
        private readonly Compiler _mo2Compiler;

        public IncludePropertyFiles(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (Compiler) compiler;
        }

        public override Directive Run(RawSourceFile source)
        {
            var files = new HashSet<string>
            {
                _mo2Compiler.ModListImage, _mo2Compiler.ModListReadme
            };
            if (!files.Any(f => source.AbsolutePath.Equals(f))) return null;
            if (!File.Exists(source.AbsolutePath)) return null;
            var isBanner = source.AbsolutePath == _mo2Compiler.ModListImage;
            //var isReadme = source.AbsolutePath == ModListReadme;
            var result = source.EvolveTo<PropertyFile>();
            result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            if (isBanner)
            {
                result.Type = PropertyType.Banner;
                _mo2Compiler.ModListImage = result.SourceDataID;
            }
            else
            {
                result.Type = PropertyType.Readme;
                _mo2Compiler.ModListReadme = result.SourceDataID;
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