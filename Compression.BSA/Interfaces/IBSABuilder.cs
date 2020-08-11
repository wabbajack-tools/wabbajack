using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
