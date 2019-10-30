using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeRegex : ACompilationStep
    {
        private readonly Regex _regex;

        public IncludeRegex(Compiler compiler, string pattern) : base(compiler)
        {
            _regex = new Regex(pattern);
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!_regex.IsMatch(source.Path)) return null;
            
            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            return result;
        }
    }
}
