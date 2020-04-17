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
        
        private static void CreateSignature(Stream oldData, FileStream sigStream)
        {
            Utils.Status("Creating Patch Signature");
            var signatureBuilder = new SignatureBuilder();
            signatureBuilder.Build(oldData, new SignatureWriter(sigStream));
            sigStream.Position = 0;
        }
        
        public static void Create(Stream oldData, FileStream newData, FileStream signature, FileStream output)
        {
            CreateSignature(oldData, signature);
            var db = new DeltaBuilder {ProgressReporter = reporter};
            db.BuildDelta(newData, new SignatureReader(signature, reporter), new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(output)));
        }

        private class ProgressReporter : IProgressReporter
        {
            private DateTime _lastUpdate = DateTime.UnixEpoch;
            private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(100);
            public void ReportProgress(string operation, long currentPosition, long total)
            {
                if (DateTime.Now - _lastUpdate < _updateInterval) return;
                _lastUpdate = DateTime.Now;
                if (currentPosition >= total || total < 1 || currentPosition < 0)
                    return;
                Utils.Status(operation, new Percent(total, currentPosition));
            }
        }

        public static void Apply(Stream input, Func<Stream> openPatchStream, Stream output)
        {
            using var deltaStream = openPatchStream();
            var deltaApplier = new DeltaApplier();
            deltaApplier.Apply(input, new BinaryDeltaReader(deltaStream, reporter), output);
        }
        
        public static void Apply(FileStream input, FileStream patchStream, FileStream output)
        {
            var deltaApplier = new DeltaApplier();
            deltaApplier.Apply(input, new BinaryDeltaReader(patchStream, reporter), output);
        }
    }
}
