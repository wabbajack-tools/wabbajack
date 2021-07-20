using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.CLI.Verbs
{
    [Verb("upload-mod-to-cdn", HelpText = "Compresses and uploads one or more folders in MO2 onto the CDN and updates the MO2's downloads folder to match.")]
    public class UploadModToCDN : AVerb
    {
        [Option('m', "mods", Required = true, HelpText = @"ModOrganizer2 mod names to export")]
        public IEnumerable<string> Mods { get; set; } = Array.Empty<string>();

        [Option('f', "filename", Required = true, HelpText = @"Human friendly filename for the created download")]
        public string Filename { get; set; } = "";
        
        [Option('r', "root", Required = true, HelpText = @"The root MO2 folder")]
        public string _MO2Path { get; set; } = "";

        public AbsolutePath MO2Path => (AbsolutePath)_MO2Path;
        protected override async Task<ExitCode> Run()
        {
            var ini = MO2Path.Combine(Consts.ModOrganizer2Ini).LoadIniFile();
            var downloadsFolder = (AbsolutePath)(ini.Settings.download_directory ?? MO2Path.Combine("downloads"));
            var fileFixed = downloadsFolder.Combine(Filename).ReplaceExtension(new Extension(".7z"));

            var folders = Mods.Select(m => MO2Path.Combine("mods", m)).ToArray();

            Utils.Log("Compressing files");
            await FileExtractor2.CompressFiles(fileFixed, folders, Utils.Log);
            Utils.Log($"Final Size: {fileFixed.Size.ToFileSizeString()}");

            Utils.Log("Uploading to CDN");
            var queue = new WorkQueue();
            var url = await (await Client.Create()).UploadFile(queue, fileFixed,(s, p) => Utils.Log($"{p} - {s}"));
            Utils.Log("Updating Meta");
            await fileFixed.WithExtension(new Extension(Consts.MetaFileExtension)).WriteAllLinesAsync(
                "[General]",
                "installed=true",
                $"directURL={url}");

            return ExitCode.Ok;
        }
    }
}
