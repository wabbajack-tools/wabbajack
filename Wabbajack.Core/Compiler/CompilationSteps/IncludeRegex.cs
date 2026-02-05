using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludeRegex : ACompilationStep
{
    private readonly string _pattern;
    private readonly Regex _regex;

    public IncludeRegex(ACompiler compiler, string pattern) : base(compiler)
    {
        _pattern = pattern;
        _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!_regex.IsMatch((string) source.Path)) return null;

        var result = source.EvolveTo<InlineFile>();
        result.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
        return result;
    }
}