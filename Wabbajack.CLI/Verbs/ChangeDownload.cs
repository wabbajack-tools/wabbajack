using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.CLI.Verbs
{
    [Verb("change-download", HelpText = "Move or Copy all used Downloads from a Modlist to another directory")]
    public class ChangeDownload : AVerb
    {
        [IsDirectory(Exit = true, CustomMessage = "Downloads folder %1 does not exist!")]
        [Option("input", Required = true, HelpText = "Input folder containing the downloads you want to move")]
        public string? Input { get; set; }

        [IsDirectory(Create = true)]
        [Option("output", Required = true, HelpText = "Output folder the downloads should be transferred to")]
        public string? Output { get; set; }

        [IsFile(Exit = true, CustomMessage = "Modlist file %1 does not exist!")]
        [Option("modlist", Required = true, HelpText = "The Modlist, can either be a .wabbajack or a modlist.txt file")]
        public string? Modlist { get; set; }

        [Option("mods", Required = false, HelpText = "Mods folder location if the provided modlist file is an MO2 modlist.txt")]
        public string? Mods { get; set; }

        [Option("copy", Default = true, HelpText = "Whether to copy the files", SetName = "copy")]
        public bool Copy { get; set; }

        [Option("move", Default = false, HelpText = "Whether to move the files", SetName = "move")]
        public bool Move { get; set; }

        [Option("overwrite", Default = false, HelpText = "Whether to overwrite the file if it already exists")]
        public bool Overwrite { get; set; }

        [Option("meta", Default = true, HelpText = "Whether to also transfer the meta file for the archive")]
        public bool IncludeMeta { get; set; }

        private struct TransferFile
        {
            public readonly string Input;
            public readonly string Output;
            public readonly bool IsMeta;

            public TransferFile(string input, string output, bool isMeta = false)
            {
                Input = input;
                Output = output;
                IsMeta = isMeta;
            }
        }

        protected override async Task<int> Run()
        {
            if (!CLIUtils.HasValidArguments(this))
                CLIUtils.Exit("Arguments are not valid!", -1);

            if (Modlist != null && (!Modlist.EndsWith(Consts.ModListExtension) && !Modlist.EndsWith("modlist.txt")))
                return CLIUtils.Exit($"The file {Modlist} is not a valid modlist file!", -1);

            if (Copy && Move)
                return CLIUtils.Exit("You can't set both copy and move flags!", -1);

            var isModlist = Modlist != null && Modlist.EndsWith(Consts.ModListExtension);

            var list = new List<TransferFile>();

            if (isModlist)
            {
                ModList modlist;

                try
                {
                    modlist = AInstaller.LoadFromFile(Modlist);
                }
                catch (Exception e)
                {
                    return CLIUtils.Exit($"Error while loading the Modlist!\n{e}", 1);
                }

                if (modlist == null)
                {
                    return CLIUtils.Exit("The Modlist could not be loaded!", 1);
                }

                CLIUtils.Log($"Modlist contains {modlist.Archives.Count} archives.");

                modlist.Archives.Do(a =>
                {
                    var inputPath = Path.Combine(Input, a.Name);
                    var outputPath = Path.Combine(Output, a.Name);

                    if (!File.Exists(inputPath))
                    {
                        CLIUtils.Log($"File {inputPath} does not exist, skipping.");
                        return;
                    }

                    CLIUtils.Log($"Adding {inputPath} to the transfer list.");
                    list.Add(new TransferFile(inputPath, outputPath));

                    var metaInputPath = Path.Combine(inputPath, ".meta");
                    var metaOutputPath = Path.Combine(outputPath, ".meta");

                    if (File.Exists(metaInputPath))
                    {
                        CLIUtils.Log($"Found meta file {metaInputPath}");
                        if (IncludeMeta)
                        {
                            CLIUtils.Log($"Adding {metaInputPath} to the transfer list.");
                            list.Add(new TransferFile(metaInputPath, metaOutputPath));
                        }
                        else
                        {
                            CLIUtils.Log($"Meta file {metaInputPath} will be ignored.");
                        }
                    }
                    else
                    {
                        CLIUtils.Log($"Found no meta file for {inputPath}");
                        if (IncludeMeta)
                        {
                            if (string.IsNullOrWhiteSpace(a.Meta))
                            {
                                CLIUtils.Log($"Meta for {a.Name} is empty, this should not be possible but whatever.");
                                return;
                            }

                            CLIUtils.Log("Adding meta from archive info the transfer list");
                            list.Add(new TransferFile(a.Meta, metaOutputPath, true));
                        }
                        else
                        {
                            CLIUtils.Log($"Meta will be ignored for {a.Name}");
                        }
                    }
                });
            }
            else
            {
                if (!Directory.Exists(Mods))
                    return CLIUtils.Exit($"Mods directory {Mods} does not exist!", -1);

                CLIUtils.Log($"Reading modlist.txt from {Modlist}");
                string[] modlist = File.ReadAllLines(Modlist);

                if (modlist == null || modlist.Length == 0)
                    return CLIUtils.Exit($"Provided modlist.txt file at {Modlist} is empty or could not be read!", -1);

                var mods = modlist.Where(s => s.StartsWith("+")).Select(s => s.Substring(1)).ToHashSet();

                if (mods.Count == 0)
                    return CLIUtils.Exit("Counted mods from modlist.txt are 0!", -1);

                CLIUtils.Log($"Found {mods.Count} mods in modlist.txt");

                var downloads = new HashSet<string>();

                Directory.EnumerateDirectories(Mods, "*", SearchOption.TopDirectoryOnly)
                    .Where(d => mods.Contains(Path.GetRelativePath(Path.GetDirectoryName(d), d)))
                    .Do(d =>
                    {
                        var meta = Path.Combine(d, "meta.ini");
                        if (!File.Exists(meta))
                        {
                            CLIUtils.Log($"Mod meta file {meta} does not exist, skipping");
                            return;
                        }

                        string[] ini = File.ReadAllLines(meta);
                        if (ini == null || ini.Length == 0)
                        {
                            CLIUtils.Log($"Mod meta file {meta} could not be read or is empty!");
                            return;
                        }

                        ini.Where(i => !string.IsNullOrWhiteSpace(i) && i.StartsWith("installationFile="))
                            .Select(i => i.Replace("installationFile=", ""))
                            .Do(i =>
                            {
                                CLIUtils.Log($"Found installationFile {i}");
                                downloads.Add(i);
                            });
                    });

                CLIUtils.Log($"Found {downloads.Count} installationFiles from mod metas.");

                Directory.EnumerateFiles(Input, "*", SearchOption.TopDirectoryOnly)
                    .Where(f => downloads.Contains(Path.GetFileNameWithoutExtension(f)))
                    .Do(f =>
                    {
                        CLIUtils.Log($"Found archive {f}");

                        var outputPath = Path.Combine(Output, Path.GetFileName(f));

                        CLIUtils.Log($"Adding {f} to the transfer list");
                        list.Add(new TransferFile(f, outputPath));

                        var metaInputPath = Path.Combine(f, ".meta");
                        if (File.Exists(metaInputPath))
                        {
                            CLIUtils.Log($"Found meta file for {f} at {metaInputPath}");
                            if (IncludeMeta)
                            {
                                var metaOutputPath = Path.Combine(outputPath, ".meta");
                                CLIUtils.Log($"Adding {metaInputPath} to the transfer list.");
                                list.Add(new TransferFile(metaInputPath, metaOutputPath));
                            }
                            else
                            {
                                CLIUtils.Log("Meta file will be ignored");
                            }
                        }
                        else
                        {
                            CLIUtils.Log($"Found no meta file for {f}");
                        }
                    });
            }

            CLIUtils.Log($"Transfer list contains {list.Count} items");
            var success = 0;
            var failed = 0;
            var skipped = 0;
            list.Do(f =>
            {
                if (File.Exists(f.Output))
                {
                    if (Overwrite)
                    {
                        CLIUtils.Log($"Output file {f.Output} already exists, it will be overwritten");
                        if (f.IsMeta || Move)
                        {
                            CLIUtils.Log($"Deleting file at {f.Output}");
                            try
                            {
                                File.Delete(f.Output);
                            }
                            catch (Exception e)
                            {
                                CLIUtils.Log($"Could not delete file {f.Output}!\n{e}");
                                failed++;
                            }
                        }
                    }
                    else
                    {
                        CLIUtils.Log($"Output file {f.Output} already exists, skipping");
                        skipped++;
                        return;
                    }
                }

                if (f.IsMeta)
                {
                    CLIUtils.Log($"Writing meta data to {f.Output}");
                    try
                    {
                        File.WriteAllText(f.Output, f.Input, Encoding.UTF8);
                        success++;
                    }
                    catch (Exception e)
                    {
                        CLIUtils.Log($"Error while writing meta data to {f.Output}!\n{e}");
                        failed++;
                    }
                }
                else
                {
                    if (Copy)
                    {
                        CLIUtils.Log($"Copying file {f.Input} to {f.Output}");
                        try
                        {
                            File.Copy(f.Input, f.Output, Overwrite ? CopyOptions.None : CopyOptions.FailIfExists, CopyMoveProgressHandler, null);
                            success++;
                        }
                        catch (Exception e)
                        {
                            CLIUtils.Log($"Error while copying file {f.Input} to {f.Output}!\n{e}");
                            failed++;
                        }
                    }
                    else if(Move)
                    {
                        CLIUtils.Log($"Moving file {f.Input} to {f.Output}");
                        try
                        {
                            File.Move(f.Input, f.Output, Overwrite ? MoveOptions.ReplaceExisting : MoveOptions.None, CopyMoveProgressHandler, null);
                            success++;
                        }
                        catch (Exception e)
                        {
                            CLIUtils.Log($"Error while moving file {f.Input} to {f.Output}!\n{e}");
                            failed++;
                        }
                    }
                }
            });

            CLIUtils.Log($"Skipped transfers: {skipped}");
            CLIUtils.Log($"Failed transfers: {failed}");
            CLIUtils.Log($"Successful transfers: {success}");

            return 0;
        }

        private static CopyMoveProgressResult CopyMoveProgressHandler(long totalfilesize, long totalbytestransferred, long streamsize, long streambytestransferred, int streamnumber, CopyMoveProgressCallbackReason callbackreason, object userdata)
        {
            Console.Write($"\r{' ', 100}");

            Console.Write(totalfilesize == totalbytestransferred
                ? "\rTransfer complete!\n"
                : $"\rTotal Size: {totalfilesize}, Transferred: {totalbytestransferred}");
            return CopyMoveProgressResult.Continue;
        }
    }
}
