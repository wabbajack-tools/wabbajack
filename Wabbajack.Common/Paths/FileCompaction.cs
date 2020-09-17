using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common.IO;

namespace Wabbajack.Common
{
    public static class FileCompaction
    {
        private static AbsolutePath _compactExecutable;
        private static bool? _haveCompact = null;

        private static AbsolutePath? GetCompactPath()
        {
            if (_haveCompact != null && _haveCompact.Value) return _compactExecutable;
            if (_haveCompact != null) return null;
            _compactExecutable = ((AbsolutePath)KnownFolders.SystemX86.Path).Combine("compact.exe");
            
            if (!_compactExecutable.Exists) return null;

            _haveCompact = true;
            return _compactExecutable;
        }
        
        public enum Algorithm
        {
            XPRESS4K,
            XPRESS8K,
            XPRESS16K,
            LZX
        }

        public static async Task<bool> Compact(this AbsolutePath path, Algorithm algorithm)
        {
            if (!path.Exists) return false;
            
            
            var exe = GetCompactPath();
            if (exe == null) return false;

            if (path.IsFile)
            {
                var proc = new ProcessHelper
                {
                    Path = exe.Value,
                    Arguments = new object[] {"/C", "/EXE:" + algorithm, path},
                    ThrowOnNonZeroExitCode = false
                };
                return await proc.Start() == 0;
            }
            else
            {
                var proc = new ProcessHelper
                {
                    Path = exe.Value,
                    Arguments = new object[] {"/C", "/S", "/EXE:" + algorithm, path},
                    ThrowOnNonZeroExitCode = false
                };
                return await proc.Start() == 0;
            }
        }

        public static async Task CompactFolder(this AbsolutePath folder, WorkQueue queue, Algorithm algorithm)
        {
            var driveInfo = folder.DriveInfo().DiskSpaceInfo;
            var clusterSize = driveInfo.SectorsPerCluster * driveInfo.BytesPerSector;

            await folder
                .EnumerateFiles(true)
                .Where(f => f.Size > clusterSize)
                .PMap(queue, async path =>
                {
                    Utils.Status($"Compacting {path.FileName}");
                    await path.Compact(algorithm);
                });
        }
    }
}
