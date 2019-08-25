using Compression.BSA;
using ICSharpCode.SharpZipLib.Zip;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class FileExtractor
    {
        static FileExtractor()
        {
            _7ZipLock = new object ();
        }

        public class Entry
        {
            public string Name;
            public ulong Size;
        }

        public static void Extract(string file, Func<Entry, Stream> f, bool leave_open = false)
        {
            if (Path.GetExtension(file) == ".bsa")
            {
                ExtractAsBSA(file, f, leave_open);
            }
            else if (Path.GetExtension(file) == ".zip")
            {
                ExtractViaNetZip(file, f, leave_open);
            }
            else
            {
                ExtractVia7Zip(file, f, leave_open);
            }
        }

        private static void ExtractAsBSA(string file, Func<Entry, Stream> f, bool leave_open)
        {
            using (var ar = new BSAReader(file))
            {
                foreach (var entry in ar.Files)
                {
                    var stream = f(new Entry()
                    {
                        Name = entry.Path,
                        Size = (ulong)entry.Size
                    });
                    if (stream == null) continue;

                    var data = entry.GetData();
                    stream.Write(data, 0, data.Length);

                    if (!leave_open)
                    {
                        stream.Flush();
                        stream.Close();
                    }
                }
            }
        }


        private static void ExtractVia7Zip(string file, Func<Entry, Stream> f, bool leave_open)
        {
            // 7Zip uses way too much memory so for now, let's limit it a bit
            lock(_7ZipLock)
            using (var af = new ArchiveFile(file))
            {
                af.Extract(entry =>
                {
                    if (entry.IsFolder) return null;
                    return f(new Entry()
                    {
                        Name = entry.FileName,
                        Size = entry.Size
                    });
                }, leave_open);
            }
        }

        private const int ZIP_BUFFER_SIZE = 1024 * 8;
        private static object _7ZipLock;

        private static void ExtractViaNetZip(string file, Func<Entry, Stream> f, bool leave_open)
        {
            using (var s = new ZipFile(File.OpenRead(file)))
            {
                s.IsStreamOwner = true;
                s.UseZip64 = UseZip64.On;

                if (s.OfType<ZipEntry>().FirstOrDefault(e => !e.CanDecompress) != null)
                {
                    ExtractVia7Zip(file, f, leave_open);
                    return;
                }

                foreach (ZipEntry entry in s)
                {
                    if (!entry.IsFile) continue;
                    var stream = f(new Entry()
                    {
                        Name = entry.Name.Replace('/', '\\'),
                        Size = (ulong)entry.Size
                    });

                    if (stream == null) continue;

                    using (var instr = s.GetInputStream(entry))
                    {
                        instr.CopyTo(stream);
                    }

                    if (!leave_open) stream.Dispose();

                }
            }
        }

        /// <summary>
        /// Returns true if the given extension type can be extracted
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool CanExtract(string v)
        {
            return Consts.SupportedArchives.Contains(v) || v == ".bsa";
        }

        // Probably replace this with VFS?
        /*
        public static void DeepExtract(string file, IEnumerable<FromArchive> files, Func<FromArchive, Entry, Stream> fnc, bool leave_open = false, int depth = 1)
        {
            // Files we need to extract at this level
            var files_for_level = files.Where(f => f.ArchiveHashPath.Length == depth)
                                       .GroupBy(e => e.From)
                                       .ToDictionary(e => e.Key);
            // Archives we need to extract at this level
            var archives_for_level = files.Where(f => f.ArchiveHashPath.Length > depth)
                                          .GroupBy(f => f.ArchiveHashPath[depth])
                                          .ToDictionary(f => f.Key);

            var disk_archives = new Dictionary<string, string>();

            Extract(file, e =>
            {
                Stream a = Stream.Null;
                Stream b = Stream.Null;

                if (files_for_level.TryGetValue(e.Name, out var fe))
                {
                    foreach (var inner_fe in fe)
                    {
                        var str = fnc(inner_fe, e);
                        if (str == null) continue;
                        a = new SplittingStream(a, false, fnc(inner_fe, e), leave_open);
                    }
                }
                
                if (archives_for_level.TryGetValue(e.Name, out var archive))
                {
                    var name = Path.GetTempFileName() + Path.GetExtension(e.Name);
                    if (disk_archives.ContainsKey(e.Name))
                    {

                    }
                    disk_archives.Add(e.Name, name);
                    b = File.OpenWrite(name);
                }

                if (a == null && b == null) return null;

                return new SplittingStream(a, leave_open, b, false);

            });

            foreach (var archive in disk_archives)
            {
                DeepExtract(archive.Value, archives_for_level[archive.Key], fnc, leave_open, depth + 1);
                File.Delete(archive.Value);
            }

        }
        */
    }
}
