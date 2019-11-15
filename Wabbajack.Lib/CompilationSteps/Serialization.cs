using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public static class Serialization
    {
        public static string Serialize(IEnumerable<ICompilationStep> stack)
        {
            return stack.Select(s => s.GetState()).ToList()
                .ToJSON(TypeNameHandling.Auto, TypeNameAssemblyFormatHandling.Simple);
        }

        public static List<ICompilationStep> Deserialize(string stack, ACompiler compiler)
        {
            return stack.FromJSONString<List<IState>>(TypeNameHandling.Auto, TypeNameAssemblyFormatHandling.Simple)
                .Select(s => s.CreateStep(compiler)).ToList();
        }
    }
}