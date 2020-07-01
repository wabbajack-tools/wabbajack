using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeGenericGamePlugin : ACompilationStep
    {
        private readonly bool _validGame;
        private readonly string _pluginsFolder = string.Empty;
        private readonly string _gameName = string.Empty;
        
        public IncludeGenericGamePlugin(ACompiler compiler) : base(compiler)
        {
            if (!(compiler is MO2Compiler mo2Compiler))
                return;

            if (mo2Compiler.CompilingGame.NexusName == null)
                return;

            _validGame = mo2Compiler.CompilingGame.IsGenericMO2Plugin;
            _pluginsFolder = mo2Compiler.MO2Folder.Combine("plugins").ToString();
            _gameName = $"game_{mo2Compiler.CompilingGame.NexusName}.py";
        }

        private static Regex regex = new Regex(@"^game_$");

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!_validGame)
                return null;

            if (!source.AbsolutePath.ToString().StartsWith(_pluginsFolder))
                return null;

            if(!source.AbsolutePath.FileName.ToString().Equals(_gameName, StringComparison.InvariantCultureIgnoreCase))
                return null;

            var res = source.EvolveTo<InlineFile>();
            res.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
            return res;
        }
    }
}
