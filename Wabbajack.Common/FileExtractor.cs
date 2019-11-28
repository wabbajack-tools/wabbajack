using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Alphaleonis.Win32.Filesystem;
using Compression.BSA;
using ICSharpCode.SharpZipLib.GZip;
using OMODExtraction;

namespace Wabbajack.Common
{
    public class FileExtractor
    {
        static FileExtractor()
        {
            ExtractResource("Wabbajack.Common.7z.dll.gz", "7z.dll");
            ExtractResource("Wabbajack.Common.7z.exe.gz", "7z.exe");
            //ExtractResource("Wabbajack.Common.innounp.exe.gz", "innounp.exe");
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


        public static void ExtractAll(WorkQueue queue, string source, string dest)
        {
            try
            {
                if (Consts.SupportedBSAs.Any(b => source.ToLower().EndsWith(b)))
                    ExtractAllWithBSA(queue, source, dest);
                else if (source.EndsWith(".omod"))
                    ExtractAllWithOMOD(source, dest);
                else
                    ExtractAllWith7Zip(source, dest);
            }
            catch (Exception ex)
            {
                Utils.Log($"Error while extracting {source}");
                throw ex;
            }
        }

        private static void ExtractAllWithOMOD(string source, string dest)
        {
            Utils.Log($"Extracting {Path.GetFileName(source)}");
            if (!Directory.Exists(dest))
                Directory.CreateDirectory(dest);
            Framework.TempDir = dest;
            var omod = new OMOD(source);
            omod.ExtractDataFiles();
            omod.ExtractPlugins();
        }

        private static void ExtractAllWithBSA(WorkQueue queue, string source, string dest)
        {
            try
            {
                using (var arch = BSADispatch.OpenRead(source))
                {
                    arch.Files
                        .PMap(queue, f =>
                        {
                            var path = f.Path;
                            if (f.Path.StartsWith("\\"))
                                path = f.Path.Substring(1);
                            Utils.Status($"Extracting {path}");
                            var outPath = Path.Combine(dest, path);
                            var parent = Path.GetDirectoryName(outPath);

                            if (!Directory.Exists(parent))
                                Directory.CreateDirectory(parent);

                            using (var fs = File.OpenWrite(outPath))
                            {
                                f.CopyDataTo(fs);
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"While Extracting {source}");
                throw ex;
            }
        }

        private static void ExtractAllWith7Zip(string source, string dest)
        {
            Utils.Log($"Extracting {Path.GetFileName(source)}");

            var info = new ProcessStartInfo
            {
                FileName = "7z.exe",
                Arguments = $"x -bsp1 -y -o\"{dest}\" \"{source}\"",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = new Process
            {
                StartInfo = info
            };

            p.Start();
            ChildProcessTracker.AddProcess(p);
            try
            {
                p.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception)
            {
            }

            var name = Path.GetFileName(source);
            try
            {
                while (!p.HasExited)
                {
                    var line = p.StandardOutput.ReadLine();
                    if (line == null)
                        break;
                    
                    if (line.Length <= 4 || line[3] != '%') continue;
                    
                    int.TryParse(line.Substring(0, 3), out var percent);
                    Utils.Status($"Extracting {name} - {line.Trim()}", percent);
                }
            }
            catch (Exception)
            {
            }

            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Utils.Log(p.StandardOutput.ReadToEnd());
                Utils.Log($"Extraction error extracting {source}");
            }
        }

        /// <summary>
        ///     Returns true if the given extension type can be extracted
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool CanExtract(string v)
        {
            v = v.ToLower();
            return Consts.SupportedArchives.Contains(v) || Consts.SupportedBSAs.Contains(v);
        }
    }
}
