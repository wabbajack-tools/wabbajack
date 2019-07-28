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
        const string TestDir = "c:\\Mod Organizer 2\\mods";
        static void Main(string[] args)
        {
            foreach (var bsa in Directory.EnumerateFiles(TestDir, "*.bsa", SearchOption.AllDirectories))
            {
                Console.WriteLine($"From {bsa}");
                using (var a = new BSAReader(bsa))
                {

                    Parallel.ForEach(a.Files, file =>
                    {
                        var abs_name = Path.Combine("c:\\tmp\\out", file.Path);

                        if (!Directory.Exists(Path.GetDirectoryName(abs_name)))
                            Directory.CreateDirectory(Path.GetDirectoryName(abs_name));

                        using (var fs = File.OpenWrite(abs_name))
                            file.CopyDataTo(fs);
                    });

                    using (var w = new BSABuilder())
                    {
                        w.ArchiveFlags = a.ArchiveFlags;
                        w.FileFlags = a.FileFlags;
                        w.HeaderType = a.HeaderType;

                        foreach (var file in a.Files)
                        {
                            var abs_path = Path.Combine("c:\\tmp\\out", file.Path);
                            using (var str = File.OpenRead(abs_path))
                                w.AddFile(file.Path, str);

                        }

                        w.RegenFolderRecords();

                        w.Build("c:\\tmp\\built.bsa");

                    }
                    break;
                }
            }
        }
    }
}
