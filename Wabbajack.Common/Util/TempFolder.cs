using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class TempFolder : IAsyncDisposable
    {
        public AbsolutePath Dir { get; }
        public bool DeleteAfter = true;

        public TempFolder(bool deleteAfter = true)
        {
            Dir = new AbsolutePath(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            if (!Dir.Exists) 
                Dir.CreateDirectory();
            DeleteAfter = deleteAfter;
        }

        public TempFolder(AbsolutePath dir, bool deleteAfter = true)
        {
            Dir = dir;
            if (!dir.Exists)
            {
                Dir.Create();
            }
            DeleteAfter = deleteAfter;
        }
        public async ValueTask DisposeAsync()
        {
            if (DeleteAfter && Dir.Exists)
            {
                await Utils.DeleteDirectory(Dir);
            }
        }
    }
}
