using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeLootFiles : ACompilationStep
    {
        private readonly string _prefix;

        public IncludeLootFiles(Compiler compiler) : base(compiler)
        {
            _prefix = Consts.LOOTFolderFilesDir + "\\";
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith(_prefix)) return null;
            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath).ToBase64());
            return result;

        }
    }
}
