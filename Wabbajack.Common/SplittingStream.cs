using System;
using System.IO;

namespace Wabbajack.Common
{
    public class SplittingStream : Stream
    {
        private readonly Stream _a;
        private readonly Stream _b;
        private readonly bool _leaveAOpen;
        private readonly bool _leaveBOpen;

        public SplittingStream(Stream a, bool leaveAOpen, Stream b, bool leaveBOpen)
        {
            _a = a;
            _b = b;
            _leaveAOpen = leaveAOpen;
            _leaveBOpen = leaveBOpen;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override void Flush()
        {
            _a.Flush();
            _b.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _a.Write(buffer, offset, count);
            _b.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_leaveAOpen) _a.Dispose();
                if (!_leaveBOpen) _b.Dispose();
            }
        }
    }
}