using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public class DiskSpaceWatcher
    {
        private CancellationToken _token;
        private long _minSpace;
        private Action<DriveInfo> _onFailure;
        private DriveInfo[] _drives;

        public DiskSpaceWatcher(CancellationToken token, IEnumerable<AbsolutePath> paths, long minSpace, Action<DriveInfo> onFailure)
        {
            _token = token;
            _minSpace = minSpace;
            _onFailure = onFailure;
            _drives = paths.Select(p => p.DriveInfo()).DistinctBy(d => d.Name).ToArray();
        }

        public async Task Start()
        {
            Utils.Trace($"Drive watcher is starting. Will warn at {(_minSpace * 2).ToFileSizeString()} and error at {_minSpace.ToFileSizeString()}");
            foreach (var drive in _drives)
            {
                Utils.Log($"Starting Drive watcher on {drive.Name} currently {drive.AvailableFreeSpace.ToFileSizeString()} free out of {drive.TotalSize.ToFileSizeString()}");
            }

            
            while (true)
            {
                foreach (var drive in _drives)
                {
                    var used = drive.AvailableFreeSpace;
                    if (used < _minSpace)
                    {
                        _onFailure(drive);
                        Utils.Fatal(new Exception($"Out of space on drive {drive.Name}"));
                    }

                    if (used < _minSpace * 2)
                    {
                        Utils.Warn($"Warning! Drive {drive.Name} only has {used.ToFileSizeString()} of free space left, processing will stop when you only have {used.ToFileSizeString()}");
                    }
                }

                if (_token.IsCancellationRequested)
                {
                    break;
                }

                await Task.Delay(1000, _token);
            }
        }

    }
}
