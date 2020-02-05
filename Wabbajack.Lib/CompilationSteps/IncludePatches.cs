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
        private readonly Dictionary<string, IGrouping<string, VirtualFile>> _indexed;
        private VirtualFile _bsa;
        private Dictionary<string, VirtualFile> _indexedByName;

        public IncludePatches(ACompiler compiler, VirtualFile constructingFromBSA = null) : base(compiler)
        {
            _bsa = constructingFromBSA;
            _indexed = _compiler.IndexedFiles.Values
                .SelectMany(f => f)
                .GroupBy(f => Path.GetFileName(f.Name).ToLower())
                .ToDictionary(f => f.Key);
            _indexedByName = _indexed.Values
                                     .SelectMany(s => s)
                                     .Where(f => f.IsNative)
                                     .ToDictionary(f => Path.GetFileName(f.FullPath));
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            var name = Path.GetFileName(source.File.Name.ToLower());
            string nameWithoutExt = name;
            if (Path.GetExtension(name) == ".mohidden")
                nameWithoutExt = Path.GetFileNameWithoutExtension(name);

            if (!_indexed.TryGetValue(Path.GetFileName(name), out var choices))
                _indexed.TryGetValue(Path.GetFileName(nameWithoutExt), out choices);

            dynamic mod_ini;
            if (_bsa == null)
                mod_ini = ((MO2Compiler)_compiler).ModMetas.FirstOrDefault(f => source.Path.StartsWith(f.Key)).Value;
            else
            {
                var bsa_path = _bsa.FullPath.RelativeTo(((MO2Compiler)_compiler).MO2Folder);
                mod_ini = ((MO2Compiler)_compiler).ModMetas.FirstOrDefault(f => bsa_path.StartsWith(f.Key)).Value;
            }

            var installationFile = mod_ini?.General?.installationFile;

            VirtualFile found = null;
            
            // Find based on exact file name + ext
            if (choices != null)
            {
                found = choices.FirstOrDefault(
                    f => Path.GetFileName(f.FilesInFullPath.First().Name) == installationFile);
            }

            // Find based on file name only (not ext)
            if (found == null && choices != null)
            {
                found = choices.OrderBy(f => f.NestingFactor)
                               .ThenByDescending(f => (f.FilesInFullPath.First() ?? f).LastModified)
                               .First();
            }

            // Find based on matchAll=<archivename> in [General] in meta.ini
            var matchAllName = (string)mod_ini?.General?.matchAll;
            if (matchAllName != null)
            {
                matchAllName = matchAllName.Trim();
                if (_indexedByName.TryGetValue(matchAllName, out var arch))
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
                e.PatchID = _compiler.IncludeFile(data);

            return e;
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
