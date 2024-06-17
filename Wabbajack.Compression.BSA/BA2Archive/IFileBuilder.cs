using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Compression.BSA.BA2Archive;

internal interface IFileBuilder
{
    uint FileHash { get; }
    uint DirHash { get; }
    string FullName { get; }

    int Index { get; }

    ValueTask WriteData(BinaryWriter wtr, CancellationToken token);
    void WriteHeader(BinaryWriter wtr, CancellationToken token);
}