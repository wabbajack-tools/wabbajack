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
        private static Task _cleanTask;

        static TempFolder()
        {
            _cleanTask = "tmp_files".RelativeTo(AbsolutePath.EntryPoint).DeleteDirectory();
        }

        public static async Task EnsureInited()
        {
            Utils.Log("Cleaning temp files");
            await _cleanTask;
        }

        public TempFolder(bool deleteAfter = true)
        {
            _cleanTask.Wait();
            Dir = Path.Combine("tmp_files", Guid.NewGuid().ToString()).RelativeTo(AbsolutePath.EntryPoint);
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
            Utils.Log($"Deleting {Dir}");
            if (DeleteAfter && Dir.Exists)
            {
                await Utils.DeleteDirectory(Dir);
            }
        }
    }
}
