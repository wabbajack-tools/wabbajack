using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreSaveFiles : MO2CompilationStep
    {
        private AbsolutePath[] _profilePaths;

        public IgnoreSaveFiles(ACompiler compiler) : base(compiler)
        {
            _profilePaths =
                MO2Compiler.SelectedProfiles.Select(p => MO2Compiler.SourcePath.Combine("profiles", p, "saves")).ToArray();
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!_profilePaths.Any(p => source.File.AbsoluteName.InFolder(p)))
                return null;

            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = "Ignore Save files";
            return result;
        }
    }
}
