using System;
using Wabbajack.Common;

namespace VirtualFileSystem.Test
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Utils.SetLoggerFn(s => Console.WriteLine(s));
            Utils.SetStatusFn((s, i) => Console.Write(s + "\r"));
            WorkQueue.Init((a, b, c) => { },
                (a, b) => { });
            VFS.VirtualFileSystem.VFS.AddRoot(@"D:\tmp\archivetests");
        }
    }
}