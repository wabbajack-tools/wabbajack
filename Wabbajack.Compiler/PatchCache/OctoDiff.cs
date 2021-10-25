using System.IO;
using Octodiff.Core;
using Octodiff.Diagnostics;

namespace Wabbajack.Compiler.PatchCache;

public class OctoDiff
{
    public static void Create(byte[] oldData, byte[] newData, Stream output)
    {
        using var signature = CreateSignature(oldData);
        using var oldStream = new MemoryStream(oldData);
        using var newStream = new MemoryStream(newData);
        var db = new DeltaBuilder();
        db.BuildDelta(newStream, new SignatureReader(signature, new NullProgressReporter()),
            new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(output)));
    }

    private static Stream CreateSignature(byte[] oldData)
    {
        using var oldDataStream = new MemoryStream(oldData);
        var sigStream = new MemoryStream();
        var signatureBuilder = new SignatureBuilder();
        signatureBuilder.Build(oldDataStream, new SignatureWriter(sigStream));
        sigStream.Position = 0;
        return sigStream;
    }

    private static void CreateSignature(Stream oldData, Stream sigStream)
    {
        var signatureBuilder = new SignatureBuilder();
        signatureBuilder.Build(oldData, new SignatureWriter(sigStream));
        sigStream.Position = 0;
    }

    public static void Create(Stream oldData, Stream newData, Stream signature, Stream output)
    {
        CreateSignature(oldData, signature);
        var db = new DeltaBuilder {ProgressReporter = new NullProgressReporter()};
        db.BuildDelta(newData, new SignatureReader(signature, new NullProgressReporter()),
            new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(output)));
    }
}