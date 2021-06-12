using System;
using System.IO;
using System.Threading.Tasks;

namespace Compression.BSA
{
    public class StreamView : Stream
    {
        private Stream _base;
        private long _startPos;
        private long _length;

        public StreamView(Stream baseStream, long startPos, long length)
        {
            _base = baseStream;
            _startPos = startPos;
            _length = length;
        }
        
        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var realCount = Math.Min(count, Length - Position);
            return _base.Read(buffer, offset, (int)realCount);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    return Position;
                case SeekOrigin.End:
                    Position = _length - offset;
                    return Position;
                case SeekOrigin.Current:
                    Position += offset;
                    return Position;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get
            {
                return _base.Position - _startPos;
            }
            set
            {
                _base.Position = _startPos + value;
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await _base.DisposeAsync();
        }
    }
}
