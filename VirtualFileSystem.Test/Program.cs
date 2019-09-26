using System;
using Wabbajack.Common;
using Microsoft.Win32;

namespace VirtualFileSystem.Test
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var result = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\7-zip\");

            Utils.SetLoggerFn(s => Console.WriteLine(s));
            Utils.SetStatusFn((s, i) => Console.Write(s + "\r"));
            WorkQueue.Init((a, b, c) => { },
                (a, b) => { });
            VFS.VirtualFileSystem.VFS.AddRoot(@"D:\tmp\archivetests");
        }
    }
}