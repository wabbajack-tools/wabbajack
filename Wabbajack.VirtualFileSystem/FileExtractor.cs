using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Compression.BSA;
using OMODFramework;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Common.StatusFeed.Errors;
using Wabbajack.Common;
using Utils = Wabbajack.Common.Utils;


namespace Wabbajack.VirtualFileSystem
{
    public class FileExtractor
    {

        public static async Task ExtractAll(WorkQueue queue, AbsolutePath source, AbsolutePath dest)
        {
            try
            {
                if (Consts.SupportedBSAs.Contains(source.Extension))
                    await ExtractAllWithBSA(queue, source, dest);
                else if (source.Extension == Consts.OMOD)
                    ExtractAllWithOMOD(source, dest);
                else if (source.Extension == Consts.EXE)
                    await ExtractAllExe(source, dest);
                else
                    await ExtractAllWith7Zip(source, dest);
            }
            catch (Exception ex)
            {
                Utils.ErrorThrow(ex, $"Error while extracting {source}");
            }
        }

        private static async Task ExtractAllExe(AbsolutePath source, AbsolutePath dest)
        {
            var isArchive = await TestWith7z(source);

            if (isArchive)
            {
                await ExtractAllWith7Zip(source, dest);
                return;
            }

            Utils.Log($"Extracting {(string)source.FileName}");

            var process = new ProcessHelper
            {
                Path = @"Extractors\innounp.exe".RelativeTo(AbsolutePath.EntryPoint),
                Arguments = new object[] {"-x", "-y", "-b", $"-d\"{dest}\"", source}
            };

            
            var result = process.Output.Where(d => d.Type == ProcessHelper.StreamType.Output)
                .ForEachAsync(p =>
                {
                    var (_, line) = p;
                    if (line == null)
                        return;

                    if (line.Length <= 4 || line[3] != '%')
                        return;

                    int.TryParse(line.Substring(0, 3), out var percentInt);
                    Utils.Status($"Extracting {source.FileName} - {line.Trim()}", Percent.FactoryPutInRange(percentInt / 100d));
                });
            await process.Start();
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

        private static void ExtractAllWithOMOD(AbsolutePath source, AbsolutePath dest)
        {
            Utils.Log($"Extracting {(string)source.FileName}");

            Framework.Settings.TempPath = (string)dest;
            Framework.Settings.CodeProgress = new OMODProgress();

            var omod = new OMOD((string)source);
            omod.GetDataFiles();
            omod.GetPlugins();
        }


        private static async Task ExtractAllWithBSA(WorkQueue queue, AbsolutePath source, AbsolutePath dest)
        {
            try
            {
                using var arch = BSADispatch.OpenRead(source);
                await arch.Files
                    .PMap(queue, f =>
                    {
                        Utils.Status($"Extracting {(string)f.Path}");
                        var outPath = f.Path.RelativeTo(dest);
                        var parent = outPath.Parent;

                        if (!parent.IsDirectory)
                            parent.CreateDirectory();

                        using var fs = outPath.Create();
                        f.CopyDataTo(fs);
                    });
            }
            catch (Exception ex)
            {
                Utils.ErrorThrow(ex, $"While Extracting {source}");
            }
        }

        private static async Task ExtractAllWith7Zip(AbsolutePath source, AbsolutePath dest)
        {
            Utils.Log(new GenericInfo($"Extracting {(string)source.FileName}", $"The contents of {(string)source.FileName} are being extracted to {(string)source.FileName} using 7zip.exe"));

            
            var process = new ProcessHelper
            {
                Path = @"Extractors\7z.exe".RelativeTo(AbsolutePath.EntryPoint),
                Arguments = new object[] {"x", "-bsp1", "-y", $"-o\"{dest}\"", source, "-mmt=off"}
            };
            

            var result = process.Output.Where(d => d.Type == ProcessHelper.StreamType.Output)
                .ForEachAsync(p =>
                {
                    var (_, line) = p;
                    if (line == null)
                        return;

                    if (line.Length <= 4 || line[3] != '%') return;

                    int.TryParse(line.Substring(0, 3), out var percentInt);
                    Utils.Status($"Extracting {(string)source.FileName} - {line.Trim()}", Percent.FactoryPutInRange(percentInt / 100d));
                });

            var exitCode = await process.Start();

            
            if (exitCode != 0)
            {
                Utils.Error(new _7zipReturnError(exitCode, source, dest, ""));
            }
            else
            {
                Utils.Status($"Extracting {source.FileName} - done", Percent.One, alsoLog: true);
            }
        }

        /// <summary>
        ///     Returns true if the given extension type can be extracted
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static async Task<bool> CanExtract(AbsolutePath v)
        {
            var ext = v.Extension;
            if(ext != _exeExtension && !Consts.TestArchivesBeforeExtraction.Contains(ext))
                return Consts.SupportedArchives.Contains(ext) || Consts.SupportedBSAs.Contains(ext);

            var isArchive = await TestWith7z(v);

            if (isArchive)
                return true;

            var process = new ProcessHelper
            {
                Path = @"Extractors\innounp.exe".RelativeTo(AbsolutePath.EntryPoint),
                Arguments = new object[] {"-t", v},
            };

            return await process.Start() == 0;
        }

        public static async Task<bool> TestWith7z(AbsolutePath file)
        {
            var process = new ProcessHelper()
            {
                Path = @"Extractors\7z.exe".RelativeTo(AbsolutePath.EntryPoint),
                Arguments = new object[] {"t", file},
            };

            return await process.Start() == 0;
        }
        
        private static Extension _exeExtension = new Extension(".exe");
        
        public static bool MightBeArchive(AbsolutePath path)
        {
            var ext = path.Extension;
            return ext == _exeExtension || Consts.SupportedArchives.Contains(ext) || Consts.SupportedBSAs.Contains(ext);
        }
    }
}
