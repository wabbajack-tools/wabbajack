using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    class IncludeAllConfigs : ACompilationStep
    {
        public IncludeAllConfigs(Compiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!Consts.ConfigFileExtensions.Contains(Path.GetExtension(source.Path))) return null;
            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            return result;
        }
    }
}
