using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeThisProfile : ACompilationStep
    {
        private readonly IEnumerable<string> _correctProfiles;

        public IncludeThisProfile(Compiler compiler) : base(compiler)
        {
            _correctProfiles = _compiler.SelectedProfiles.Select(p => Path.Combine("profiles", p) + "\\").ToList();
        }

        public override Directive Run(RawSourceFile source)
        {
            if (_correctProfiles.Any(p => source.Path.StartsWith(p)))
            {
                var data = source.Path.EndsWith("\\modlist.txt")
                    ? ReadAndCleanModlist(source.AbsolutePath)
                    : File.ReadAllBytes(source.AbsolutePath);

                var e = source.EvolveTo<InlineFile>();
                e.SourceDataID = _compiler.IncludeFile(data);
                return e;
            }

            return null;
        }

        public override IState GetState()
        {
            return new State();
        }

        private static byte[] ReadAndCleanModlist(string absolutePath)
        {
            var lines = File.ReadAllLines(absolutePath);
            lines = (from line in lines
                where !(line.StartsWith("-") && !line.EndsWith("_separator"))
                select line).ToArray();
            return Encoding.UTF8.GetBytes(string.Join("\r\n", lines));
        }

        [JsonObject("IncludeThisProfile")]
        public class State : IState
        {
            public ICompilationStep CreateStep(Compiler compiler)
            {
                return new IncludeThisProfile(compiler);
            }
        }
    }
}