using System;
using Wabbajack.Common;
using Microsoft.Win32;
using System.Reactive;

namespace VirtualFileSystem.Test
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var result = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\7-zip\");

            Utils.LogMessages.Subscribe(s => Console.WriteLine(s));
            Utils.StatusUpdates.Subscribe((i) => Console.Write(i.Message + "\r"));
            VFS.VirtualFileSystem.VFS.AddRoot(@"D:\tmp\archivetests");
        }
    }
}