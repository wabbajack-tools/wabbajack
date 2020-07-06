using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using F23.StringSimilarity;
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
        private bool _isGenericGame;

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

            _isGenericGame = _mo2Compiler.CompilingGame.IsGenericMO2Plugin;
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (_isGenericGame)
            {
                if (source.Path.StartsWith(Consts.GameFolderFilesDir))
                    return null;
            }

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

            VirtualFile[] found = {};
            
            // Find based on exact file name + ext
            if (choices != null && installationFile != null)
            {
                var relName = (RelativePath)Path.GetFileName(installationFile);
                found = choices.Where(f => f.FilesInFullPath.First().Name.FileName == relName).ToArray();
            }

            // Find based on file name only (not ext)
            if (found.Length == 0 && choices != null)
            {
                found = choices.ToArray();
            }

            // Find based on matchAll=<archivename> in [General] in meta.ini
            var matchAllName = (string?)modIni?.General?.matchAll;
            if (matchAllName != null && found.Length == 0)
            {
                var relName = (RelativePath)Path.GetFileName(matchAllName);
                if (_indexedByName.TryGetValue(relName, out var arch))
                {
                    var dist = new Levenshtein();
                    found = arch.SelectMany(a => a.ThisAndAllChildren)
                        .OrderBy(a => dist.Distance(a.FullPath.FileName.ToString(), source.File.FullPath.FileName.ToString()))
                        .Take(3)
                        .ToArray();
                }
            }

            if (found.Length == 0)
                return null;

            
            var e = source.EvolveTo<PatchedFromArchive>();

            var patches = found.Select(c => (Utils.TryGetPatch(c.Hash, source.File.Hash, out var data), data, c))
                .ToArray();

            if (patches.All(p => p.Item1))
            {
                var (_, bytes, file) = patches.OrderBy(f => f.data!.Length).First();
                e.FromHash = file.Hash;
                e.ArchiveHashPath = file.MakeRelativePaths();
                e.PatchID = await _compiler.IncludeFile(bytes!);
            }
            else
            {
                e.Choices = found;
            }
            
            return e;
        }

        private AbsolutePath ModForFile(AbsolutePath file)
        {
            return file.RelativeTo(((MO2Compiler)_compiler).MO2ModsFolder).TopParent
                .RelativeTo(((MO2Compiler)_compiler).MO2ModsFolder);
        }
    }
}
