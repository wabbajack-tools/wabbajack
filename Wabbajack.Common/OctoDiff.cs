using System;
using System.IO;
using Octodiff.Core;
using Octodiff.Diagnostics;

namespace Wabbajack.Common
{
    public class OctoDiff
    {
        private static ProgressReporter reporter = new ProgressReporter();
        public static void Create(byte[] oldData, byte[] newData, Stream output)
        {
            using var signature = CreateSignature(oldData);
            using var oldStream = new MemoryStream(oldData);
            using var newStream = new MemoryStream(newData);
            var db = new DeltaBuilder {ProgressReporter = reporter};
            db.BuildDelta(newStream, new SignatureReader(signature, reporter), new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(output)));
        }

        private static Stream CreateSignature(byte[] oldData)
        {
            Utils.Status("Creating Patch Signature");
            using var oldDataStream = new MemoryStream(oldData);
            var sigStream = new MemoryStream();
            var signatureBuilder = new SignatureBuilder();
            signatureBuilder.Build(oldDataStream, new SignatureWriter(sigStream));
            sigStream.Position = 0;
            return sigStream;
        }

        private class ProgressReporter : IProgressReporter
        {
            public void ReportProgress(string operation, long currentPosition, long total)
            {
                Utils.Status(operation, new Percent(total, currentPosition));
            }
        }

        public static void Apply(Stream input, Func<Stream> openPatchStream, Stream output)
        {
            using var deltaStream = openPatchStream();
            var deltaApplier = new DeltaApplier();
            deltaApplier.Apply(input, new BinaryDeltaReader(deltaStream, reporter), output);
        }
    }
}
