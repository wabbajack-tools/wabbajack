using System;
using Wabbajack.Common;

namespace VirtualFileSystem.Test
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Utils.SetLoggerFn(s => Console.WriteLine(s));
            Utils.SetStatusFn((s, i) => Console.WriteLine(s));
            WorkQueue.Init((a, b, c) => { },
                (a, b) => { });
            VFS.VirtualFileSystem.VFS.AddRoot(@"D:\tmp\Interesting NPCs SSE 3.42\Data");
        }
    }
}