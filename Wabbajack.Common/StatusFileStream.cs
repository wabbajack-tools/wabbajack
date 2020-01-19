using System.IO;

namespace Wabbajack.Common
{
    public class StatusFileStream : Stream
    {
        private string _message;
        private Stream _inner;
        private WorkQueue _queue;

        public StatusFileStream(Stream fs, string message, WorkQueue queue = null)
        {
            _queue = queue;
            _inner = fs;
            _message = message;
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            UpdateStatus();
            return _inner.Read(buffer, offset, count);
        }

        private void UpdateStatus()
        {
            if (_inner.Length == 0)
            {
                return;
            }

            if (_queue != null)
                _queue.Report(_message, (int) (_inner.Position * 100 / _inner.Length));
            else
                Utils.Status(_message, (int) (_inner.Position * 100 / _inner.Length));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            UpdateStatus();
            _inner.Write(buffer, offset, count);
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }
    }
}
