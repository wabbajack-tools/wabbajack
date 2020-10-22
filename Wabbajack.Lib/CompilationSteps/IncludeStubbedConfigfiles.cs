using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeStubbedConfigFiles : ACompilationStep
    {
        private readonly MO2Compiler _mo2Compiler;

        public IncludeStubbedConfigFiles(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            return Consts.ConfigFileExtensions.Contains(source.Path.Extension) ? await RemapFile(source) : null;
        }
        private async Task<Directive?> RemapFile(RawSourceFile source)
        {
            var data = await source.AbsolutePath.ReadAllTextAsync();
            var originalData = data;

            data = data.Replace((string)_mo2Compiler.GamePath, Consts.GAME_PATH_MAGIC_BACK);
            data = data.Replace(((string)_mo2Compiler.GamePath).Replace("\\", "\\\\"), Consts.GAME_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(((string)_mo2Compiler.GamePath).Replace("\\", "/"), Consts.GAME_PATH_MAGIC_FORWARD);

            data = data.Replace((string)_mo2Compiler.SourcePath, Consts.MO2_PATH_MAGIC_BACK);
            data = data.Replace(((string)_mo2Compiler.SourcePath).Replace("\\", "\\\\"), Consts.MO2_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(((string)_mo2Compiler.SourcePath).Replace("\\", "/"), Consts.MO2_PATH_MAGIC_FORWARD);

            data = data.Replace((string)_mo2Compiler.DownloadsPath, Consts.DOWNLOAD_PATH_MAGIC_BACK);
            data = data.Replace(((string)_mo2Compiler.DownloadsPath).Replace("\\", "\\\\"),
                Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(((string)_mo2Compiler.DownloadsPath).Replace("\\", "/"), Consts.DOWNLOAD_PATH_MAGIC_FORWARD);

            if (data == originalData)
                return null;
            var result = source.EvolveTo<RemappedInlineFile>();
            result.SourceDataID = await _compiler.IncludeFile(Encoding.UTF8.GetBytes(data));
            return result;
        }
    }
}
