using System.IO;
using System.Threading.Tasks;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Common
{
    public class TempStream : FileStream
    {
        private TempFile _file;

        public TempStream(TempFile file) : base(file.File.FullName, FileMode.Create, FileAccess.ReadWrite)
        {
            _file = file;
        }

        public TempStream() : this(new TempFile())
        {
            
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _file.DisposeAsync().AsTask().Wait();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _file.DisposeAsync();
        }
    }
}
