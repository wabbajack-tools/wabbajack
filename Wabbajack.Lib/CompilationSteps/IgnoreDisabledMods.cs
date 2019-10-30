using System.Collections.Generic;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreDisabledMods : ACompilationStep
    {
        private readonly IEnumerable<string> _allEnabledMods;

        public IgnoreDisabledMods(Compiler compiler) : base(compiler)
        {
            var alwaysEnabled = _compiler.ModInis.Where(f => IsAlwaysEnabled(f.Value)).Select(f => f.Key).ToHashSet();

            _allEnabledMods = _compiler.SelectedProfiles
                .SelectMany(p => File.ReadAllLines(Path.Combine(_compiler.MO2Folder, "profiles", p, "modlist.txt")))
                .Where(line => line.StartsWith("+") || line.EndsWith("_separator"))
                .Select(line => line.Substring(1))
                .Concat(alwaysEnabled)
                .Select(line => Path.Combine("mods", line) + "\\")
                .ToList();
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith("mods") || _allEnabledMods.Any(mod => source.Path.StartsWith(mod)))
                return null;
            var r = source.EvolveTo<IgnoredDirectly>();
            r.Reason = "Disabled Mod";
            return r;
        }


        private static bool IsAlwaysEnabled(dynamic data)
        {
            if (data == null)
                return false;
            if (data.General != null && data.General.notes != null &&
                data.General.notes.Contains(
                    Consts.WABBAJACK_ALWAYS_ENABLE))
                return true;
            if (data.General != null && data.General.comments != null &&
                data.General.notes.Contains(Consts.WABBAJACK_ALWAYS_ENABLE))
                return true;
            return false;
        }
    }
}
