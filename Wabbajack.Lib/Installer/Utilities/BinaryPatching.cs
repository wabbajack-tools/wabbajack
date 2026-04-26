using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octodiff.Core;
using Octodiff.Diagnostics;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.Installer.Utilities;

public class BinaryPatching
{
    public static async ValueTask<Hash> ApplyPatch(Stream input, Stream deltaStream, Stream output, CancellationToken? token = null)
    {
        var deltaApplier = new DeltaApplier();
        deltaApplier.Apply(input, new BinaryDeltaReader(deltaStream, new NullProgressReporter()), output);
        output.Position = 0;
        return await output.Hash(token ?? CancellationToken.None);
    }
}