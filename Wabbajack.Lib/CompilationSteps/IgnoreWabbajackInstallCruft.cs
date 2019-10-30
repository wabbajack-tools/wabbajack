using System.Collections.Generic;
using System.Linq;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    class IgnoreWabbajackInstallCruft : ACompilationStep
    {
        private readonly HashSet<string> _cruftFiles;

        public IgnoreWabbajackInstallCruft(Compiler compiler) : base(compiler)
        { 
            _cruftFiles = new HashSet<string>
            {
                "7z.dll", "7z.exe", "vfs_staged_files\\", "nexus.key_cache", "patch_cache\\",
                Consts.NexusCacheDirectory + "\\"
            };
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!_cruftFiles.Any(f => source.Path.StartsWith(f))) return null;
            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = "Wabbajack Cruft file";
            return result;
        }
    }
}
