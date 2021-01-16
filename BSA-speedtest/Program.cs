using System;
using System.Diagnostics;
using System.Linq;
using Compression.BSA;
using Wabbajack.Common;

namespace BSA_speedtest
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < 10000; i++)
            {
                var bsa = BSAReader.Load(new AbsolutePath(@"G:\Skyrim - Textures3.bsa"));
                var files = bsa.Files.ToArray();
                var names = files.Select(f => f.Path.ToString()).ToArray();
                if (i % 100 == 0)
                {
                    System.Console.WriteLine(i);
                }
            }
            sw.Stop();
            System.Console.WriteLine($"{sw.ElapsedMilliseconds}ms");
            System.Console.ReadLine();
        }
    }
}
