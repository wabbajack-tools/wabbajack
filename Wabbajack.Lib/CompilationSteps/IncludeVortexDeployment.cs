using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeVortexDeployment : ACompilationStep
    {
        public IncludeVortexDeployment(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            // * TODO I don't know what this does
            /*
            var l = new List<string> {"vortex.deployment.msgpack", "vortex.deployment.json"};
            if (!l.Any(a => ((string)source.Path).Contains(a))) return null;
            var inline = source.EvolveTo<InlineFile>();
            inline.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            if (!((string)source.Path).Contains("vortex.deployment.json"))
                return inline;

            var path = source.Path;
            if (!path.StartsWith(Consts.GameFolderFilesDir))
                return inline;
            */
            //path = ((string)path).Substring(Consts.GameFolderFilesDir.Length + 1);
            //path = $"{Consts.ManualGameFilesDir}\\{path}";
            //inline.To = path;

            return null;
        }

        public override IState GetState()
        {
            return new State();
        }

        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeVortexDeployment(compiler);
            }
        }
    }
}
