using System;
using System.IO;
using System.Threading.Tasks;
using AlphaPath = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{
    public class TempFile : IAsyncDisposable
    {
        public FileInfo File { get; private set; }
        public AbsolutePath Path => (AbsolutePath)File.FullName;
        public bool DeleteAfter = true;

        public TempFile(bool deleteAfter = true, bool createFolder = true)
            : this(new FileInfo((string)GetTempFilePath()))
        {
        }
        
        public TempFile(AbsolutePath path, bool deleteAfter = true, bool createFolder = true)
            : this(new FileInfo((string)path))
        {
        }

        private static AbsolutePath GetTempFilePath()
        {
            var path = (@"temp\" + Guid.NewGuid()).RelativeTo(AbsolutePath.EntryPoint).WithExtension(Consts.TempExtension);
            path.Parent.CreateDirectory();
            return path;
        }

        public TempFile(FileInfo file, bool deleteAfter = true, bool createFolder = true)
        {
            this.File = file;
            if (createFolder && file.Directory != null && !file.Directory!.Exists)
            {
                file.Directory.Create();
            }
            this.DeleteAfter = deleteAfter;
        }

        public TempFile(Extension ext)
        :this(new FileInfo((string)GetTempFilePath().WithExtension(ext)))
        {
        }

        public async ValueTask DisposeAsync()
        {
            if (DeleteAfter && Path.Exists)
            {
                await Path.DeleteAsync();
            }
        }
    }
}
