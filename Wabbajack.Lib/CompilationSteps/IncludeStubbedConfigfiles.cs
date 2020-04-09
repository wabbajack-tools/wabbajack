using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;
#nullable enable

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

        public override IState GetState()
        {
            return new State();
        }

        private async Task<Directive?> RemapFile(RawSourceFile source)
        {
            var data = await source.AbsolutePath.ReadAllTextAsync();
            var originalData = data;

            data = data.Replace((string)_mo2Compiler.GamePath, Consts.GAME_PATH_MAGIC_BACK);
            data = data.Replace(((string)_mo2Compiler.GamePath).Replace("\\", "\\\\"), Consts.GAME_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(((string)_mo2Compiler.GamePath).Replace("\\", "/"), Consts.GAME_PATH_MAGIC_FORWARD);

            data = data.Replace((string)_mo2Compiler.MO2Folder, Consts.MO2_PATH_MAGIC_BACK);
            data = data.Replace(((string)_mo2Compiler.MO2Folder).Replace("\\", "\\\\"), Consts.MO2_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(((string)_mo2Compiler.MO2Folder).Replace("\\", "/"), Consts.MO2_PATH_MAGIC_FORWARD);

            data = data.Replace((string)_mo2Compiler.MO2DownloadsFolder, Consts.DOWNLOAD_PATH_MAGIC_BACK);
            data = data.Replace(((string)_mo2Compiler.MO2DownloadsFolder).Replace("\\", "\\\\"),
                Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(((string)_mo2Compiler.MO2DownloadsFolder).Replace("\\", "/"), Consts.DOWNLOAD_PATH_MAGIC_FORWARD);

            if (data == originalData)
                return null;
            var result = source.EvolveTo<RemappedInlineFile>();
            result.SourceDataID = await _compiler.IncludeFile(Encoding.UTF8.GetBytes(data));
            return result;
        }

        [JsonObject("IncludeStubbedConfigFiles")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeStubbedConfigFiles(compiler);
            }
        }
    }
}
