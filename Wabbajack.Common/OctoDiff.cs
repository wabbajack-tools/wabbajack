using System;
using System.IO;
using Octodiff.Core;
using Octodiff.Diagnostics;

namespace Wabbajack.Common
{
    public class OctoDiff
    {
        public static void Create(byte[] oldData, byte[] newData, Stream output)
        {
            using var signature = CreateSignature(oldData);
            using var oldStream = new MemoryStream(oldData);
            using var newStream = new MemoryStream(newData);
            var db = new DeltaBuilder {ProgressReporter = new ProgressReporter()};
            db.BuildDelta(newStream, new SignatureReader(signature, new ProgressReporter()), new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(output)));
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
        
        private static void CreateSignature(Stream oldData, Stream sigStream)
        {
            Utils.Status("Creating Patch Signature");
            var signatureBuilder = new SignatureBuilder();
            signatureBuilder.Build(oldData, new SignatureWriter(sigStream));
            sigStream.Position = 0;
        }
        
        public static void Create(Stream oldData, Stream newData, Stream signature, Stream output, ProgressReporter? reporter = null)
        {
            CreateSignature(oldData, signature);
            var db = new DeltaBuilder {ProgressReporter = reporter ?? new ProgressReporter()};
            db.BuildDelta(newData, new SignatureReader(signature, reporter ?? new ProgressReporter()), new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(output)));
        }

        public class ProgressReporter : IProgressReporter
        {
            private DateTime _lastUpdate = DateTime.UnixEpoch;
            private TimeSpan _updateInterval;
            private Action<string, Percent> _report;

            public ProgressReporter()
            {
                _updateInterval = TimeSpan.FromMilliseconds(100);
                _report = (s, percent) => Utils.Status(s, percent);
            }
            
            public ProgressReporter(TimeSpan updateInterval, Action<string, Percent> report)
            {
                _updateInterval = updateInterval;
                _report = report;
            }
            
             
            public void ReportProgress(string operation, long currentPosition, long total)
            {
                if (DateTime.Now - _lastUpdate < _updateInterval) return;
                _lastUpdate = DateTime.Now;
                if (currentPosition >= total || total < 1 || currentPosition < 0)
                    return;
                _report(operation, new Percent(total, currentPosition));
            }
        }

        public static void Apply(Stream input, Func<Stream> openPatchStream, Stream output)
        {
            using var deltaStream = openPatchStream();
            var deltaApplier = new DeltaApplier();
            deltaApplier.Apply(input, new BinaryDeltaReader(deltaStream, new ProgressReporter()), output);
        }
        
        public static void Apply(FileStream input, FileStream patchStream, FileStream output)
        {
            var deltaApplier = new DeltaApplier();
            deltaApplier.Apply(input, new BinaryDeltaReader(patchStream, new ProgressReporter()), output);
        }
    }
}
