using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;
#nullable enable

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeThisProfile : ACompilationStep
    {
        private readonly IEnumerable<AbsolutePath> _correctProfiles;
        private MO2Compiler _mo2Compiler;

        public IncludeThisProfile(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
            _correctProfiles = _mo2Compiler.SelectedProfiles.Select(p => _mo2Compiler.MO2ProfileDir.Parent.Combine(p)).ToList();
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!_correctProfiles.Any(p => source.AbsolutePath.InFolder(p)))
                return null;

            var data = source.Path.FileName == Consts.ModListTxt
                ? await ReadAndCleanModlist(source.AbsolutePath)
                : await source.AbsolutePath.ReadAllBytesAsync();

            var e = source.EvolveTo<InlineFile>();
            e.SourceDataID = await _compiler.IncludeFile(data);
            return e;

        }

        public override IState GetState()
        {
            return new State();
        }

        private static async Task<byte[]> ReadAndCleanModlist(AbsolutePath absolutePath)
        {
            var lines = await absolutePath.ReadAllLinesAsync();
            lines = lines.Where(line => !(line.StartsWith("-") && !line.EndsWith("_separator"))).ToArray();
            return Encoding.UTF8.GetBytes(string.Join("\r\n", lines));
        }

        [JsonObject("IncludeThisProfile")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeThisProfile(compiler);
            }
        }
    }
}
