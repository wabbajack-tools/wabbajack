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

        public static AbsolutePath BaseFolder => AbsolutePath.EntryPoint.Combine("tmp_files");

        static TempFolder()
        {
            _cleanTask = Task.Run(() => BaseFolder.DeleteDirectory());
        }

        /// <summary>
        /// Starts the initialization in a background task
        /// </summary>
        public static void Warmup()
        {
            // Nothing to do, as work is done in static ctor
        }

        private TempFolder(bool deleteAfter = true)
        {
            Dir = BaseFolder.Combine(Guid.NewGuid().ToString());
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
