using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace VirtualFileSystem.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            WorkQueue.Init((a, b, c) => { return; },
                           (a, b) => { return; });
            var vfs = new VirtualFileSystem();
            vfs.AddRoot(@"D:\MO2 Instances\Mod Organizer 2", s => Console.WriteLine(s));
        }
    }
}
