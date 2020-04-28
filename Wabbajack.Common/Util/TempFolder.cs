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
            _cleanTask = Task.Run(() => "tmp_files".RelativeTo(AbsolutePath.EntryPoint).DeleteDirectory());
        }

        public static void Init()
        {
            // Nothing to do, as work is done in static ctor
        }

        private TempFolder(bool deleteAfter = true)
        {
            Dir = Path.Combine("tmp_files", Guid.NewGuid().ToString()).RelativeTo(AbsolutePath.EntryPoint);
            if (!Dir.Exists) 
                Dir.CreateDirectory();
            DeleteAfter = deleteAfter;
        }

        public static async Task<TempFolder> Create(bool deleteAfter = true)
        {
            await _cleanTask;
            return new TempFolder(deleteAfter: deleteAfter);
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
