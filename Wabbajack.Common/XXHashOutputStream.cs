using NeoSmart.Hashing.XXHash;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class XXHashOutputStream : Stream
    {
        private XXHash64 _hasher;

        public XXHashOutputStream()
        {
            _hasher = new XXHash64();
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
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
            _hasher.Update(buffer, offset, count);
        }

        public string Result
        {
            get
            {
                return BitConverter.GetBytes(_hasher.Result).ToBase64();
            }
        }
    }
}
