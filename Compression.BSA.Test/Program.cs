using ICSharpCode.SharpZipLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compression.BSA.Test
{
    class Program
    {
        const string TestDir = "d:\\MO2 Instances\\Mod Organizer 2 - LE";
        const string TempDir = "c:\\tmp\\out";
        static void Main(string[] args)
        {
            foreach (var bsa in Directory.EnumerateFiles(TestDir, "*.bsa", SearchOption.AllDirectories).Skip(2))
            {
                Console.WriteLine($"From {bsa}");
                Console.WriteLine("Cleaning Output Dir");
                if (Directory.Exists(TempDir))
                {
                    Directory.Delete(TempDir, true);
                }
                Directory.CreateDirectory(TempDir);

                Console.WriteLine($"Reading {bsa}");
                using (var a = new BSAReader(bsa))
                {

                    Parallel.ForEach(a.Files, file =>
                    {
                        var abs_name = Path.Combine(TempDir, file.Path);

                        if (!Directory.Exists(Path.GetDirectoryName(abs_name)))
                            Directory.CreateDirectory(Path.GetDirectoryName(abs_name));

                        using (var fs = File.OpenWrite(abs_name))
                            file.CopyDataTo(fs);
                        Equal((long)file.Size, new FileInfo(abs_name).Length);
                    });


                    Console.WriteLine($"Building {bsa}");

                    using (var w = new BSABuilder())
                    {
                        w.ArchiveFlags = a.ArchiveFlags;
                        w.FileFlags = a.FileFlags;
                        w.HeaderType = a.HeaderType;

                        Parallel.ForEach(a.Files, file =>
                        {
                            var abs_path = Path.Combine("c:\\tmp\\out", file.Path);
                            using (var str = File.OpenRead(abs_path))
                            {
                                var entry = w.AddFile(file.Path, str, file.FlipCompression);
                            }

                        });
                        
                        w.Build("c:\\tmp\\built.bsa");

                        // Sanity Checks
                        Equal(a.Files.Count(), w.Files.Count());
                        Equal(a.Files.Select(f => f.Path).ToHashSet(), w.Files.Select(f => f.Path).ToHashSet());

                        /*foreach (var pair in Enumerable.Zip(a.Files, w.Files, (ai, bi) => (ai, bi)))
                        {
                            Console.WriteLine($"{pair.ai.Path},  {pair.ai.Hash},  {pair.bi.Path}, {pair.bi.Hash}");
                        }*/

                        foreach (var pair in Enumerable.Zip(a.Files, w.Files, (ai, bi) => (ai, bi)))
                        {
                            Equal(pair.ai.Path, pair.bi.Path);
                            Equal(pair.ai.Hash, pair.bi.Hash);
                        }

                    }

                    Console.WriteLine($"Verifying {bsa}");
                    using (var b = new BSAReader("c:\\tmp\\built.bsa"))
                    {
                        Console.WriteLine($"Performing A/B tests on {bsa}");
                        Equal((uint)a.ArchiveFlags, (uint)b.ArchiveFlags);
                        Equal((uint)a.FileFlags, (uint)b.FileFlags);

                        // Check same number of files
                        Equal(a.Files.Count(), b.Files.Count());
                        int idx = 0;
                        foreach (var pair in Enumerable.Zip(a.Files, b.Files, (ai, bi) => (ai, bi)))
                        {
                            idx ++;
                            //Console.WriteLine($"   - {pair.ai.Path}");
                            Equal(pair.ai.Path, pair.bi.Path);
                            Equal(pair.ai.Compressed, pair.bi.Compressed);
                            Equal(pair.ai.Size, pair.bi.Size);
                            Equal(pair.ai.GetData(), pair.bi.GetData());

                        }
                    }
                }
            }
        }

        private static void Equal(HashSet<string> a, HashSet<string> b)
        {
            Equal(a.Count, b.Count);

            foreach (var itm in a)
                Equal(b.Contains(itm));

        }

        private static void Equal(bool v)
        {
            if (!v) throw new InvalidDataException("False");
        }

        public static void Equal(uint a, uint b)
        {
            if (a == b) return;

            throw new InvalidDataException($"{a} != {b}");
        }

        public static void Equal(long a, long b)
        {
            if (a == b) return;

            throw new InvalidDataException($"{a} != {b}");
        }

        public static void Equal(ulong a, ulong b)
        {
            if (a == b) return;

            throw new InvalidDataException($"{a} != {b}");
        }

        public static void Equal(int a, int b)
        {
            if (a == b) return;

            throw new InvalidDataException($"{a} != {b}");
        }

        public static void Equal(string a, string b)
        {
            if (a == b) return;

            throw new InvalidDataException($"{a} != {b}");
        }

        public static void Equal(bool a, bool b)
        {
            if (a == b) return;

            throw new InvalidDataException($"{a} != {b}");
        }

        public static void Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                throw new InvalidDataException($"Byte array sizes are not equal");
            }

            for (var idx = 0; idx < a.Length; idx ++)
            {
                if (a[idx] != b[idx])
                    throw new InvalidDataException($"Byte array contents not equal at {idx}");
            }
        }
    }
}
