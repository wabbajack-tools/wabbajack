using Alphaleonis.Win32.Filesystem;

namespace Wabbajack.Lib.CompilationSteps
{
    class IncludeModIniData : ACompilationStep
    {
        public IncludeModIniData(Compiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith("mods\\") || !source.Path.EndsWith("\\meta.ini")) return null;
            var e = source.EvolveTo<InlineFile>();
            e.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            return e;

        }
    }
}
