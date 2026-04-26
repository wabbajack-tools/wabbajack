using System.IO;
using System.Threading;
using Octodiff.Core;
using Octodiff.Diagnostics;
using Wabbajack.RateLimiter;

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

    public static void Create(Stream oldData, Stream newData, Stream signature, Stream output, IJob? job)
    {
        CreateSignature(oldData, signature);
        var db = new DeltaBuilder {ProgressReporter = new JobProgressReporter(job, 0)};
        db.BuildDelta(newData, new SignatureReader(signature, new JobProgressReporter(job, 100)),
            new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(output)));
    }

    private class JobProgressReporter : IProgressReporter
    {
        private readonly IJob? _job;
        private readonly int _offset;

        public JobProgressReporter(IJob? job, int offset)
        {
            _offset = offset;
            _job = job;
        }
        public void ReportProgress(string operation, long currentPosition, long total)
        {
            if (_job == default) return;
            var percent = Percent.FactoryPutInRange(currentPosition, total);
            var toReport = (long) (percent.Value * 100) - (_job.Current - _offset);
            _job.ReportNoWait((int) toReport);
        }
    }
}