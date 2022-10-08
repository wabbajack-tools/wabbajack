using System.IO;
using System.Threading.Tasks;
using Octodiff.Core;
using Octodiff.Diagnostics;

namespace Wabbajack.Installer.Utilities;

public class BinaryPatching
{
    public static ValueTask ApplyPatch(Stream input, Stream deltaStream, Stream output)
    {
        var deltaApplier = new DeltaApplier();
        deltaApplier.Apply(input, new BinaryDeltaReader(deltaStream, new NullProgressReporter()), output);
        return ValueTask.CompletedTask;
    }
}