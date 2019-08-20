using System;
using Wabbajack.Common;

namespace VirtualFileSystem.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Utils.SetLoggerFn(s => Console.WriteLine(s));
            Utils.SetStatusFn((s, i) => Console.WriteLine(s));
            WorkQueue.Init((a, b, c) => { return; },
                           (a, b) => { return; });
            VFS.VirtualFileSystem.VFS.AddRoot(@"D:\MO2 Instances\Mod Organizer 2");
        }
    }
}
