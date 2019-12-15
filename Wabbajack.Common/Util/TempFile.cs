using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class TempFile : IDisposable
    {
        public FileInfo File { get; private set; }
        public bool DeleteAfter = true;

        public TempFile(bool deleteAfter = true, bool createFolder = true)
            : this(new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())))
        {
        }

        public TempFile(FileInfo file, bool deleteAfter = true, bool createFolder = true)
        {
            this.File = file;
            if (createFolder && !file.Directory.Exists)
            {
                file.Directory.Create();
            }
            this.DeleteAfter = deleteAfter;
        }

        public void Dispose()
        {
            if (DeleteAfter)
            {
                this.File.Delete();
            }
        }
    }
}
