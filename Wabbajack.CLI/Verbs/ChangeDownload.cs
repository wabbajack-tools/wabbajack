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
        [IsDirectory(CustomMessage = "Downloads folder %1 does not exist!")]
        [Option("input", Required = true, HelpText = "Input folder containing the downloads you want to move")]
        public string _input { get; set; } = "";
        public AbsolutePath Input => (AbsolutePath)_input;


        [IsDirectory(Create = true)]
        [Option("output", Required = true, HelpText = "Output folder the downloads should be transferred to")]
        public string _output { get; set; } = "";

        public AbsolutePath Output => (AbsolutePath)_output;

        [IsFile(CustomMessage = "Modlist file %1 does not exist!")]
        [Option("modlist", Required = true, HelpText = "The Modlist, can either be a .wabbajack or a modlist.txt file")]
        public string _modlist { get; set; } = "";

        public AbsolutePath ModList => (AbsolutePath)_modlist;

        [Option("mods", Required = false,
            HelpText = "Mods folder location if the provided modlist file is an MO2 modlist.txt")]
        public string _mods { get; set; } = "";

        public AbsolutePath Mods => (AbsolutePath)_mods;

        [Option("copy", Default = true, HelpText = "Whether to copy the files", SetName = "copy")]
        public bool Copy { get; set; }

        [Option("move", Default = false, HelpText = "Whether to move the files", SetName = "move")]
        public bool Move { get; set; }

        [Option("overwrite", Default = false, HelpText = "Whether to overwrite the file if it already exists")]
        public bool Overwrite { get; set; }

        [Option("meta", Default = true, HelpText = "Whether to also transfer the meta file for the archive")]
        public bool IncludeMeta { get; set; }
        
        private interface ITransferFile
        {
            public Task PerformCopy();
            public AbsolutePath Output { get; }
        }

        private struct FileCopy : ITransferFile
        {
            private AbsolutePath _src;
            private AbsolutePath _dest;

            public FileCopy(AbsolutePath src, AbsolutePath dest)
            {
                _src = src;
                _dest = dest;
            }

            public async Task PerformCopy()
            {
                CLIUtils.Log($"Copying {_src} to {_dest}");
                await _src.CopyToAsync(_dest);
            }

            public AbsolutePath Output => _dest;
        }

        private struct StringCopy : ITransferFile
        {
            private string _src;
            private AbsolutePath _dest;

            public StringCopy(string src, AbsolutePath dest)
            {
                _src = src;
                _dest = dest;
            }

            public async Task PerformCopy()
            {
                CLIUtils.Log($"Writing data to {_src}");
                await _dest.WriteAllTextAsync(_src);
            }
            public AbsolutePath Output => _dest;

        }

        protected override async Task<ExitCode> Run()
        {
            if (ModList.Extension != Consts.ModListExtension && ModList.FileName != (RelativePath)"modlist.txt")
                return CLIUtils.Exit($"The file {ModList} is not a valid modlist file!", ExitCode.BadArguments);

            if (Copy && Move)
                return CLIUtils.Exit("You can't set both copy and move flags!", ExitCode.BadArguments);

            var isModlist = ModList.Extension == Consts.ModListExtension;

            var list = new List<ITransferFile>();

            if (isModlist)
            {
                ModList modlist;

                try
                {
                    modlist = AInstaller.LoadFromFile(ModList);
                }
                catch (Exception e)
                {
                    return CLIUtils.Exit($"Error while loading the Modlist!\n{e}", ExitCode.Error);
                }

                if (modlist == null)
                {
                    return CLIUtils.Exit("The Modlist could not be loaded!", ExitCode.Error);
                }

                CLIUtils.Log($"Modlist contains {modlist.Archives.Count} archives.");
                
                using var queue = new WorkQueue();

                CLIUtils.Log("Hashing Downloads (this may take some time)");
                var hashes = (await Input.EnumerateFiles().PMap(queue, async f =>
                    {
                        CLIUtils.Log($"Hashing {f}");
                        return (f, await f.FileHashCachedAsync());
                    }))
                    .GroupBy(d => d.Item2)
                    .ToDictionary(d => d.Key, d => d.First().f);

                modlist.Archives.Do(a =>
                {
                    if (!hashes.TryGetValue(a.Hash, out var inputPath))
                    {
                        CLIUtils.Log($"Archive not found for hash {a.Hash}");
                        return;
                    }

                    var outputPath = Output.Combine(a.Name);

                    CLIUtils.Log($"Adding {inputPath} to the transfer list.");
                    list.Add(new FileCopy(inputPath, outputPath));

                    var metaInputPath = inputPath.WithExtension(Consts.MetaFileExtension);
                    var metaOutputPath = outputPath.WithExtension(Consts.MetaFileExtension);

                    if (metaInputPath.Exists)
                    {
                        CLIUtils.Log($"Found meta file {metaInputPath}");
                        if (IncludeMeta)
                        {
                            CLIUtils.Log($"Adding {metaInputPath} to the transfer list.");
                            list.Add(new FileCopy(metaInputPath, metaOutputPath));
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
                            list.Add(new StringCopy(a.Meta, metaOutputPath));
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
                if (!Mods.Exists)
                    return CLIUtils.Exit($"Mods directory {Mods} does not exist!", ExitCode.BadArguments);

                CLIUtils.Log($"Reading modlist.txt from {ModList}");
                var modlist = await ModList.ReadAllLinesAsync();

                if (modlist == null || !modlist.Any())
                    return CLIUtils.Exit($"Provided modlist.txt file at {ModList} is empty or could not be read!", ExitCode.BadArguments);

                var mods = modlist.Where(s => s.StartsWith("+")).Select(s => s.Substring(1)).Select(f => (RelativePath)f).ToHashSet();

                if (mods.Count == 0)
                    return CLIUtils.Exit("Counted mods from modlist.txt are 0!", ExitCode.BadArguments);

                CLIUtils.Log($"Found {mods.Count} mods in modlist.txt");

                var downloads = new HashSet<string>();

                    Mods.EnumerateDirectories(recursive:false)
                    .Where(d => mods.Contains(d.Parent.FileName))
                    .Do(async d =>
                    {
                        var meta = d.Combine("meta.ini");
                        if (!meta.Exists)
                        {
                            CLIUtils.Log($"Mod meta file {meta} does not exist, skipping");
                            return;
                        }

                        var ini = await meta.ReadAllLinesAsync();
                        if (ini == null || !ini.Any())
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

                    Input.EnumerateFiles()
                    .Where(f => downloads.Contains(f.FileNameWithoutExtension.ToString()))
                    .Do(f =>
                    {
                        CLIUtils.Log($"Found archive {f}");

                        var outputPath = Output.Combine(f.FileName);

                        CLIUtils.Log($"Adding {f} to the transfer list");
                        list.Add(new FileCopy(f, outputPath));

                        var metaInputPath = f.WithExtension(Consts.MetaFileExtension);
                        if (metaInputPath.Exists)
                        {
                            CLIUtils.Log($"Found meta file for {f} at {metaInputPath}");
                            if (IncludeMeta)
                            {
                                var metaOutputPath = outputPath.WithExtension(Consts.MetaFileExtension);
                                CLIUtils.Log($"Adding {metaInputPath} to the transfer list.");
                                list.Add(new FileCopy(metaInputPath, metaOutputPath));
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
            list.Do(async f =>
            {
                if (f.Output.Exists)
                {
                    if (Overwrite)
                    {
                        CLIUtils.Log($"Output file {f.Output} already exists, it will be overwritten");
                        if (f is StringCopy || Move)
                        {
                            CLIUtils.Log($"Deleting file at {f.Output}");
                            try
                            {
                                if (f.Output.Exists) 
                                    f.Output.Delete();
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

                await f.PerformCopy();
            });

            CLIUtils.Log($"Skipped transfers: {skipped}");
            CLIUtils.Log($"Failed transfers: {failed}");
            CLIUtils.Log($"Successful transfers: {success}");

            return 0;
        }
    }
}
