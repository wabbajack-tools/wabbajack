using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;
#nullable enable

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreDisabledMods : ACompilationStep
    {
        private readonly IEnumerable<AbsolutePath> _allEnabledMods;
        private readonly MO2Compiler _mo2Compiler;

        public IgnoreDisabledMods(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
            var alwaysEnabled = _mo2Compiler.ModInis.Where(f => IsAlwaysEnabled(f.Value)).Select(f => f.Key).Distinct();

            _allEnabledMods = _mo2Compiler.SelectedProfiles
                .SelectMany(p => _mo2Compiler.MO2Folder.Combine("profiles", p, "modlist.txt").ReadAllLines())
                .Where(line => line.StartsWith("+") || line.EndsWith("_separator"))
                .Select(line => line.Substring(1).RelativeTo(_mo2Compiler.MO2ModsFolder))
                .Concat(alwaysEnabled)
                .ToList();
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!source.AbsolutePath.InFolder(_mo2Compiler.MO2ModsFolder)) return null;
            if (_allEnabledMods.Any(mod => source.AbsolutePath.InFolder(mod)))
                return null;
            var r = source.EvolveTo<IgnoredDirectly>();
            r.Reason = "Disabled Mod";
            return r;
        }

        public override IState GetState()
        {
            return new State();
        }

        public static bool IsAlwaysEnabled(dynamic data)
        {
            if (data == null)
                return false;
            if (data.General != null && data.General.notes != null &&
                data.General.notes.Contains(
                    Consts.WABBAJACK_ALWAYS_ENABLE))
                return true;
            if (data.General != null && data.General.comments != null &&
                data.General.comments.Contains(Consts.WABBAJACK_ALWAYS_ENABLE))
                return true;
            return false;
        }

        [JsonObject("IgnoreDisabledMods")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnoreDisabledMods(compiler);
            }
        }
    }
}
