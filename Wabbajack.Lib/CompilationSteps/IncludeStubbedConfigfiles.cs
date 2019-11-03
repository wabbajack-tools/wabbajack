using System.Text;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeStubbedConfigFiles : ACompilationStep
    {
        public IncludeStubbedConfigFiles(ACompiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            return Consts.ConfigFileExtensions.Contains(Path.GetExtension(source.Path)) ? RemapFile(source) : null;
        }

        public override IState GetState()
        {
            return new State();
        }

        private Directive RemapFile(RawSourceFile source)
        {
            var data = File.ReadAllText(source.AbsolutePath);
            var originalData = data;

            data = data.Replace(_compiler.GamePath, Consts.GAME_PATH_MAGIC_BACK);
            data = data.Replace(_compiler.GamePath.Replace("\\", "\\\\"), Consts.GAME_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(_compiler.GamePath.Replace("\\", "/"), Consts.GAME_PATH_MAGIC_FORWARD);

            data = data.Replace(_compiler._mo2Compiler.MO2Folder, Consts.MO2_PATH_MAGIC_BACK);
            data = data.Replace(_compiler._mo2Compiler.MO2Folder.Replace("\\", "\\\\"), Consts.MO2_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(_compiler._mo2Compiler.MO2Folder.Replace("\\", "/"), Consts.MO2_PATH_MAGIC_FORWARD);

            data = data.Replace(_compiler._mo2Compiler.MO2DownloadsFolder, Consts.DOWNLOAD_PATH_MAGIC_BACK);
            data = data.Replace(_compiler._mo2Compiler.MO2DownloadsFolder.Replace("\\", "\\\\"),
                Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK);
            data = data.Replace(_compiler._mo2Compiler.MO2DownloadsFolder.Replace("\\", "/"), Consts.DOWNLOAD_PATH_MAGIC_FORWARD);

            if (data == originalData)
                return null;
            var result = source.EvolveTo<RemappedInlineFile>();
            result.SourceDataID = _compiler.IncludeFile(Encoding.UTF8.GetBytes(data));
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