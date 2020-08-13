using System;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders
{
    public interface IUpgradingState
    {
        /// <summary>
        /// Find a possible archive that can be combined with a server generated patch to get the input archive
        /// state;
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public Task<(Archive? Archive, TempFile NewFile)> FindUpgrade(Archive a, Func<Archive, Task<AbsolutePath>> downloadResolver);

        Task<bool> ValidateUpgrade(Hash srcHash, AbstractDownloadState newArchiveState);
    }
}
