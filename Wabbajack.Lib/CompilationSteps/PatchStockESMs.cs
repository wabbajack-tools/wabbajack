using System.IO;
using Newtonsoft.Json;
using Wabbajack.Common;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.CompilationSteps
{
    public class PatchStockESMs : ACompilationStep
    {
        private readonly Compiler _mo2Compiler;

        public PatchStockESMs(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (Compiler) compiler;
        }

        public override Directive Run(RawSourceFile source)
        {
            var filename = Path.GetFileName(source.Path);
            var gameFile = Path.Combine(_mo2Compiler.GamePath, "Data", filename);
            if (!Consts.GameESMs.Contains(filename) || !source.Path.StartsWith("mods\\") ||
                !File.Exists(gameFile)) return null;

            Utils.Log(
                $"A ESM named {filename} was found in a mod that shares a name with a core game ESMs, it is assumed this is a cleaned ESM and it will be binary patched.");
            var result = source.EvolveTo<CleanedESM>();
            result.SourceESMHash = _compiler.VFS.Index.ByRootPath[gameFile].Hash;

            Utils.Status($"Generating patch of {filename}");
            using (var ms = new MemoryStream())
            {
                Utils.CreatePatch(File.ReadAllBytes(gameFile), File.ReadAllBytes(source.AbsolutePath), ms);
                var data = ms.ToArray();
                result.SourceDataID = _compiler.IncludeFile(data);
                Utils.Log($"Generated a {data.Length} byte patch for {filename}");
            }

            return result;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("PatchStockESMs")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new PatchStockESMs(compiler);
            }
        }
    }
}