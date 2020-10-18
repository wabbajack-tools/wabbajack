using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public class NativeCompilerSettings : CompilerSettings
    {
        public Game CompilingGame { get; set; }

        public string ModListName { get; set; } = "untitled";
        
        public string[][] CompilationSteps = new string[0][];

    }
}
