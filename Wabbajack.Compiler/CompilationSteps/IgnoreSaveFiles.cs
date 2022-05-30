using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.CompilationSteps;

public class IgnoreSaveFiles : MO2CompilationStep
{
    private readonly bool _includeSaves;
    private readonly AbsolutePath _sourcePath;
    private readonly string _tag;
    private readonly AbsolutePath[] _profilePaths;

    public IgnoreSaveFiles(ACompiler compiler) : base(compiler)
    {
        _tag = Consts.WABBAJACK_INCLUDE_SAVES;
        _includeSaves = _compiler._settings.Source.EnumerateFiles(_tag).FirstOrDefault() != default;

        _profilePaths =
            MO2Compiler._settings.AllProfiles
                .Select(p => _compiler._settings.Source.Combine(Consts.MO2Profiles, p, Consts.MO2Saves)).ToArray();
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (_includeSaves)
        {
            foreach (var folderpath in _profilePaths)
            {
                if (!source.AbsolutePath.InFolder(folderpath)) continue;
                var result = source.EvolveTo<InlineFile>();
                result.SourceDataID = await _compiler.IncludeFile(source.AbsolutePath, CancellationToken.None);
                return result;
            }
        }
        else
        {
            if (!_profilePaths.Any(p => source.File.AbsoluteName.InFolder(p)))
                return null;

            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = "Ignore Save files";
            return result;
        }

        return null;
    }
}