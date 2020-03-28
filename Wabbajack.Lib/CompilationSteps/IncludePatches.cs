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
        private VirtualFile _bsa;
        private Dictionary<RelativePath, VirtualFile> _indexedByName;
        private MO2Compiler _mo2Compiler;

        public IncludePatches(ACompiler compiler, VirtualFile constructingFromBSA = null) : base(compiler)
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
                                     .ToDictionary(f => f.FullPath.FileName);
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            
            var name = source.File.Name.FileName;
            RelativePath nameWithoutExt = name;
            if (name.Extension == Consts.MOHIDDEN)
                nameWithoutExt = name.FileNameWithoutExtension;

            if (!_indexed.TryGetValue(name, out var choices))
                _indexed.TryGetValue(nameWithoutExt, out choices);

            dynamic modIni = null;
            if (source.AbsolutePath.InFolder(_mo2Compiler.MO2ModsFolder))
            {
                if (_bsa == null)
                    ((MO2Compiler)_compiler).ModInis.TryGetValue(ModForFile(source.AbsolutePath), out modIni);
                else
                {
                    var bsaPath = _bsa.FullPath.Paths.Last().RelativeTo(((MO2Compiler)_compiler).MO2Folder);
                    ((MO2Compiler)_compiler).ModInis.TryGetValue(ModForFile(bsaPath), out modIni);
                }
            }

            var installationFile = modIni?.General?.installationFile;

            VirtualFile found = null;
            
            // Find based on exact file name + ext
            if (choices != null)
            {
                found = choices.FirstOrDefault(
                    f => f.FilesInFullPath.First().Name.FileName == installationFile);
            }

            // Find based on file name only (not ext)
            if (found == null && choices != null)
            {
                found = choices.OrderBy(f => f.NestingFactor)
                               .ThenByDescending(f => (f.FilesInFullPath.First() ?? f).LastModified)
                               .First();
            }

            // Find based on matchAll=<archivename> in [General] in meta.ini
            var matchAllName = (RelativePath?)modIni?.General?.matchAll;
            if (matchAllName != null)
            {
                if (_indexedByName.TryGetValue(matchAllName.Value, out var arch))
                {
                    // Just match some file in the archive based on the smallest delta difference
                    found = arch.ThisAndAllChildren
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

            Utils.TryGetPatch(found.Hash, source.File.Hash, out var data);

            if (data != null)
                e.PatchID = await _compiler.IncludeFile(data);

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
