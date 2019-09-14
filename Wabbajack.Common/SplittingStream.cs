using System;
using System.IO;

namespace Wabbajack.Common
{
    public class SplittingStream : Stream
    {
        private readonly Stream _a;
        private readonly Stream _b;
        private readonly bool _leave_a_open;
        private readonly bool _leave_b_open;

        public SplittingStream(Stream a, bool leave_a_open, Stream b, bool leave_b_open)
        {
            _a = a;
            _b = b;
            _leave_a_open = leave_a_open;
            _leave_b_open = leave_b_open;
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
                if (!_leave_a_open) _a.Dispose();
                if (!_leave_b_open) _b.Dispose();
            }
        }
    }
}