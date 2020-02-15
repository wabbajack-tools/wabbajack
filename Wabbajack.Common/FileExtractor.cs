using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Compression.BSA;
using ICSharpCode.SharpZipLib.GZip;
using Newtonsoft.Json;
using OMODFramework;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Common.StatusFeed.Errors;

namespace Wabbajack.Common
{
    public class FileExtractor
    {

        public static async Task ExtractAll(WorkQueue queue, string source, string dest)
        {
            try
            {
                if (Consts.SupportedBSAs.Any(b => source.ToLower().EndsWith(b)))
                    await ExtractAllWithBSA(queue, source, dest);
                else if (source.EndsWith(".omod"))
                    ExtractAllWithOMOD(source, dest);
                else if (source.EndsWith(".exe"))
                    ExtractAllWithInno(source, dest);
                else
                    ExtractAllWith7Zip(source, dest);
            }
            catch (Exception ex)
            {
                Utils.ErrorThrow(ex, $"Error while extracting {source}");
            }
        }

        private static void ExtractAllWithInno(string source, string dest)
        {
            Utils.Log($"Extracting {Path.GetFileName(source)}");

            var info = new ProcessStartInfo
            {
                FileName = @"Extractors\innounp.exe",
                Arguments = $"-x -y -b -d\"{dest}\" \"{source}\"",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = new Process {StartInfo = info};

            p.Start();
            ChildProcessTracker.AddProcess(p);

            try
            {
                p.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception e)
            {
                Utils.Error(e, "Error while setting process priority level for innounp.exe");
            }

            var name = Path.GetFileName(source);
            try
            {
                while (!p.HasExited)
                {
                    var line = p.StandardOutput.ReadLine();
                    if (line == null)
                        break;

                    if (line.Length <= 4 || line[3] != '%')
                        continue;

                    int.TryParse(line.Substring(0, 3), out var percentInt);
                    Utils.Status($"Extracting {name} - {line.Trim()}", Percent.FactoryPutInRange(percentInt / 100d));
                }
            }
            catch (Exception e)
            {
                Utils.Error(e, "Error while reading StandardOutput for innounp.exe");
            }

            p.WaitForExitAndWarn(TimeSpan.FromSeconds(30), $"Extracting {name}");
            if (p.ExitCode == 0)
                return;

            Utils.Log(p.StandardOutput.ReadToEnd());
            Utils.Log($"Extraction error extracting {source}");
        }

        private class OMODProgress : ICodeProgress
        {
            private long _total;

            public void SetProgress(long inSize, long outSize)
            {
                Utils.Status("Extracting OMOD", Percent.FactoryPutInRange(inSize, _total));
            }

            public void Init(long totalSize, bool compressing)
            {
                _total = totalSize;
            }

            public void Dispose()
            {
                //
            }
        }

        private static void ExtractAllWithOMOD(string source, string dest)
        {
            Utils.Log($"Extracting {Path.GetFileName(source)}");

            Framework.Settings.TempPath = dest;
            Framework.Settings.CodeProgress = new OMODProgress();

            var omod = new OMOD(source);
            omod.GetDataFiles();
            omod.GetPlugins();
        }


        private static async Task ExtractAllWithBSA(WorkQueue queue, string source, string dest)
        {
            try
            {
                using (var arch = BSADispatch.OpenRead(source))
                {
                    await arch.Files
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

                            using (var fs = File.Open(outPath, System.IO.FileMode.Create))
                            {
                                f.CopyDataTo(fs);
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                Utils.ErrorThrow(ex, $"While Extracting {source}");
            }
        }

        private static void ExtractAllWith7Zip(string source, string dest)
        {
            Utils.Log(new GenericInfo($"Extracting {Path.GetFileName(source)}", $"The contents of {source} are being extracted to {dest} using 7zip.exe"));

            var info = new ProcessStartInfo
            {
                FileName = @"Extractors\7z.exe",
                Arguments = $"x -bsp1 -y -o\"{dest}\" \"{source}\" -mmt=off",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = new Process {StartInfo = info};

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

                    int.TryParse(line.Substring(0, 3), out var percentInt);
                    Utils.Status($"Extracting {name} - {line.Trim()}", Percent.FactoryPutInRange(percentInt / 100d));
                }
            }
            catch (Exception)
            {
            }

            p.WaitForExitAndWarn(TimeSpan.FromSeconds(30), $"Extracting {name}");

            if (p.ExitCode == 0)
            {
                Utils.Status($"Extracting {name} - 100%", Percent.One, alsoLog: true);
                return;
            }
            Utils.Error(new _7zipReturnError(p.ExitCode, source, dest, p.StandardOutput.ReadToEnd()));
        }

        /// <summary>
        ///     Returns true if the given extension type can be extracted
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool CanExtract(string v)
        {
            var ext = Path.GetExtension(v.ToLower());
            if(ext != ".exe" && !Consts.TestArchivesBeforeExtraction.Contains(ext))
                return Consts.SupportedArchives.Contains(ext) || Consts.SupportedBSAs.Contains(ext);

            if (ext == ".exe")
            {
                var info = new ProcessStartInfo
                {
                    FileName = @"Extractors\innounp.exe",
                    Arguments = $"-t \"{v}\" ",
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var p = new Process {StartInfo = info};

                p.Start();
                ChildProcessTracker.AddProcess(p);

                var name = Path.GetFileName(v);
                while (!p.HasExited)
                {
                    var line = p.StandardOutput.ReadLine();
                    if (line == null)
                        break;

                    if (line[0] != '#')
                        continue;

                    Utils.Status($"Testing {name} - {line.Trim()}");
                }

                p.WaitForExitAndWarn(TimeSpan.FromSeconds(30), $"Testing {name}");
                return p.ExitCode == 0;
            }


            var testInfo = new ProcessStartInfo
            {
                FileName = @"Extractors\7z.exe",
                Arguments = $"t \"{v}\"",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var testP = new Process {StartInfo = testInfo};

            testP.Start();
            ChildProcessTracker.AddProcess(testP);
            try
            {
                testP.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception)
            {
            }

            try
            {
                while (!testP.HasExited)
                {
                    var line = testP.StandardOutput.ReadLine();
                    if (line == null)
                        break;
                }
            } catch (Exception){}

            testP.WaitForExitAndWarn(TimeSpan.FromSeconds(30), $"Can Extract Check {v}");
            return testP.ExitCode == 0;
        }
        
        
        public static bool MightBeArchive(string path)
        {
            var ext = Path.GetExtension(path.ToLower());
            return ext == ".exe" || Consts.SupportedArchives.Contains(ext) || Consts.SupportedBSAs.Contains(ext);
        }
    }
}
