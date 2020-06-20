using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludePatches : ACompilationStep
    {
        private readonly Dictionary<RelativePath, IGrouping<RelativePath, VirtualFile>> _indexed;
        private VirtualFile? _bsa;
        private Dictionary<RelativePath, IEnumerable<VirtualFile>> _indexedByName;
        private MO2Compiler _mo2Compiler;

        public IncludePatches(ACompiler compiler, VirtualFile? constructingFromBSA = null) : base(compiler)
        {
            _bsa = constructingFromBSA;
            _mo2Compiler = (MO2Compiler)compiler;
            _indexed = _compiler.IndexedFiles.Values
                .SelectMany(f => f)
                .GroupBy(f => f.Name.FileName)
                .ToDictionary(f => f.Key);
            _indexedByName = _indexed.Values
                                     .SelectMany(s => s)
                                     .Where(f => f.IsNative)
                                     .GroupBy(f => f.FullPath.FileName)
                                     .ToDictionary(f => f.Key, f => (IEnumerable<VirtualFile>)f);
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            var name = source.File.Name.FileName;
            RelativePath nameWithoutExt = name;
            if (name.Extension == Consts.MOHIDDEN)
                nameWithoutExt = name.FileNameWithoutExtension;

            if (!_indexed.TryGetValue(name, out var choices))
                _indexed.TryGetValue(nameWithoutExt, out choices);

            dynamic? modIni = null;
            
            if (_bsa == null && source.File.IsNative && source.AbsolutePath.InFolder(_mo2Compiler.MO2ModsFolder))
                ((MO2Compiler)_compiler).ModInis.TryGetValue(ModForFile(source.AbsolutePath), out modIni);
            else if (_bsa != null)
            {
                var bsaPath = _bsa.FullPath.Base;
                ((MO2Compiler)_compiler).ModInis.TryGetValue(ModForFile(bsaPath), out modIni);
            }

            var installationFile = (string?)modIni?.General?.installationFile;

            VirtualFile? found = null;
            
            // Find based on exact file name + ext
            if (choices != null && installationFile != null)
            {
                var relName = (RelativePath)Path.GetFileName(installationFile);
                found = choices.FirstOrDefault(
                    f => f.FilesInFullPath.First().Name.FileName == relName);
            }

            // Find based on file name only (not ext)
            if (found == null && choices != null)
            {
                found = choices.OrderBy(f => f.NestingFactor)
                               .ThenByDescending(f => (f.FilesInFullPath.First() ?? f).LastModified)
                               .First();
            }

            // Find based on matchAll=<archivename> in [General] in meta.ini
            var matchAllName = (string?)modIni?.General?.matchAll;
            if (matchAllName != null)
            {
                var relName = (RelativePath)Path.GetFileName(matchAllName);
                if (_indexedByName.TryGetValue(relName, out var arch))
                {
                    // Just match some file in the archive based on the smallest delta difference
                    found = arch.SelectMany(a => a.ThisAndAllChildren)
                        .OrderBy(o => Math.Abs(o.Size - source.File.Size))
                        .First();
                }
            }

            if (found == null)
                return null;

            var e = source.EvolveTo<PatchedFromArchive>();
            e.FromHash = found.Hash;
            e.ArchiveHashPath = found.MakeRelativePaths();
            e.To = source.Path;
            e.Hash = source.File.Hash;

            if (Utils.TryGetPatch(found.Hash, source.File.Hash, out var data))
            {
                e.PatchID = await _compiler.IncludeFile(data);
            }

            return e;
        }

        private AbsolutePath ModForFile(AbsolutePath file)
        {
            return file.RelativeTo(((MO2Compiler)_compiler).MO2ModsFolder).TopParent
                .RelativeTo(((MO2Compiler)_compiler).MO2ModsFolder);
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludePatches")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludePatches(compiler);
            }
        }
    }
}
