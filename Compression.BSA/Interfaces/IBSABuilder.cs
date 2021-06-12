using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Compression.BSA
{
    public interface IBSABuilder : IAsyncDisposable
    {
        Task AddFile(FileStateObject state, Stream src);
        Task Build(AbsolutePath filename);
    }
}
