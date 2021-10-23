using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.BSA.FileStates;

namespace Wabbajack.Compression.BSA.Interfaces;

public interface IBuilder
{
    ValueTask AddFile(AFile state, Stream src, CancellationToken token);
    ValueTask Build(Stream filename, CancellationToken token);
}