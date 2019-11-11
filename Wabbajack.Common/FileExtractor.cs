using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Compression.BSA;
using ICSharpCode.SharpZipLib.GZip;
using OMODFramework;
using Wabbajack.Common.CSP;

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


        public static void ExtractAll(string source, string dest)
        {
            try
            {
                if (Consts.SupportedBSAs.Any(b => source.ToLower().EndsWith(b)))
                    ExtractAllWithBSA(source, dest).Wait();
                else if (source.EndsWith(".exe"))
                    ExtractAllWithInno(source, dest);
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
            var f = new Framework();
            f.SetTempDirectory(dest);
            var omod = new OMOD(source, ref f);
            omod.ExtractDataFiles();
            omod.ExtractPlugins();
        }

        private static async Task ExtractAllWithBSA(string source, string dest)
        {
            try
            {
                using (var arch = await BSADispatch.OpenRead(source))
                {
                    await arch.Files.ToChannel()
                                    .UnorderedPipeline(
                                        Channel.CreateSink<IFile>(), 
                                        async f =>
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
                                            await f.CopyDataToAsync(fs);
                                        }

                                        return f;
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
                    var percent = 0;
                    if (line.Length > 4 && line[3] == '%')
                    {
                        int.TryParse(line.Substring(0, 3), out percent);
                        Utils.Status($"Extracting {name} - {line.Trim()}", percent);
                    }
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

        private static void ExtractAllWithInno(string source, string dest)
        {
            Utils.Log($"Extracting {Path.GetFileName(source)}");

            var info = new ProcessStartInfo
            {
                FileName = "innounp.exe",
                Arguments = $"-x -y -b -d\"{dest}\" \"{source}\"",
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
                    var percent = 0;
                    if (line.Length > 4 && line[3] == '%')
                    {
                        int.TryParse(line.Substring(0, 3), out percent);
                        Utils.Status($"Extracting {name} - {line.Trim()}", percent);
                    }
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

        public class Entry
        {
            public string Name;
            public ulong Size;
        }
    }
}