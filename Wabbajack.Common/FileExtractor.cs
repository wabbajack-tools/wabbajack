using Compression.BSA;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Wabbajack.Common
{
    public class FileExtractor
    {
        static FileExtractor()
        {
            ExtractResource("Wabbajack.Common.7z.dll.gz", "7z.dll");
            ExtractResource("Wabbajack.Common.7z.exe.gz", "7z.exe");
        }

        private static void ExtractResource(string from, string to)
        {
            if (File.Exists(to))
                File.Delete(to);

            using (var ous = File.OpenWrite(to))
            using (var ins = new GZipInputStream(Assembly.GetExecutingAssembly().GetManifestResourceStream(from)))
            {
                ins.CopyTo(ous);
            }
        }

        public class Entry
        {
            public string Name;
            public ulong Size;
        }

       
        public static void ExtractAll(string source, string dest)
        {
            if (source.EndsWith(".bsa")) { 
                ExtractAllWithBSA(source, dest);
            }
            else
            {
                ExtractAllWith7Zip(source, dest);
            }
        }

        private static void ExtractAllWithBSA(string source, string dest)
        {
            using (var arch = new BSAReader(source))
            {
                arch.Files.PMap(f =>
                {
                    var path = f.Path;
                    if (f.Path.StartsWith("\\"))
                        path = f.Path.Substring(1);
                    Utils.Status($"Extracting {path}");
                    var out_path = Path.Combine(dest, path);
                    var parent = Path.GetDirectoryName(out_path);

                    if (!Directory.Exists(parent))
                        Directory.CreateDirectory(parent);

                    using (var fs = File.OpenWrite(out_path))
                    {
                        f.CopyDataTo(fs);
                    }
                });
            }
        }

        private static void ExtractAllWith7Zip(string source, string dest)
        {
            Utils.Log($"Extracting {Path.GetFileName(source)}");

            var info = new ProcessStartInfo
            {
                FileName = "7z.exe",
                Arguments = $"x -o\"{dest}\" \"{source}\"",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var p = new Process
            {
                StartInfo = info,
            };

            p.Start();
            ChildProcessTracker.AddProcess(p);
            p.PriorityClass = ProcessPriorityClass.BelowNormal;

            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Utils.Log(p.StandardOutput.ReadToEnd());
                Utils.Log($"Extraction error extracting {source}");
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

    }
}
