using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack
{
    public class ReportBuilder : IDisposable
    {
        private StreamWriter wtr;
        public ReportBuilder(Stream str)
        {
            wtr = new StreamWriter(str);
        }

        private const int WRAP_SIZE = 80;
        public void Text(string txt)
        {
            int offset = 0; 
            while (offset + WRAP_SIZE < txt.Length)
            {
                wtr.WriteLine(txt.Substring(offset, WRAP_SIZE));
                offset += WRAP_SIZE;
            }
            if (offset < txt.Length)
            {
                wtr.WriteLine(txt.Substring(offset, txt.Length - offset));
            }
        }

        public void NoWrapText(string txt)
        {
            wtr.WriteLine(txt);
        }

        public void Build(ModList lst)
        {
            Text($"### {lst.Name} - Installation Summary");
            Text($"#### Download Summary ({lst.Archives.Count} archives)");
            foreach (var archive in SortArchives(lst.Archives))
            {
                switch (archive)
                {
                    case NexusMod m:
                        var profile = m.UploaderProfile.Replace("/games/", "/"+NexusAPI.ConvertGameName(m.GameName).ToLower()+"/");
                        NoWrapText($"* [{m.UploadedBy}]({profile}) - [{m.Name}](http://nexusmods.com/{NexusAPI.ConvertGameName(m.GameName)}/mods/{m.ModID})");
                        break;
                    case MODDBArchive m:
                        NoWrapText($"* MODDB - [{m.Name}]({m.URL})");
                        break;
                    case MEGAArchive m:
                        NoWrapText($"* MEGA - [{m.Name}]({m.URL})");
                        break;
                    case GoogleDriveMod m:
                        NoWrapText($"* GoogleDrive - [{m.Name}](https://drive.google.com/uc?id={m.Id}&export=download)");
                        break;
                    case DirectURLArchive m:
                        NoWrapText($"* URL - [{m.Name} - {m.URL}]({m.URL})");
                        break;
                }
            }
            Text($"\n\n");
            var patched = lst.Directives.OfType<PatchedFromArchive>().OrderBy(p => p.To).ToList();
            Text($"#### Summary of ({patched.Count}) patches");
            foreach (var directive in patched)
            {
                NoWrapText($"* Applying {directive.Patch.Length} byte patch `{directive.FullPath}` to create `{directive.To}`");
            }

            
            var files = lst.Directives.OrderBy(d => d.To).ToList();
            Text($"\n\n### Install Plan of ({files.Count}) files");
            Text($"(ignoring files that are directly copied from archives or listed in the patches section above)");
            foreach (var directive in files)
            {
                switch (directive)
                {
                    case FromArchive f:
                        //NoWrapText($"* `{f.To}` from `{f.FullPath}`");
                        break;
                    case CleanedESM i:
                        NoWrapText($"* `{i.To}` by applying a patch to a game ESM ({i.SourceESMHash})");
                        break;
                    case RemappedInlineFile i:
                        NoWrapText($"* `{i.To}` by remapping the contents of a inline file");
                        break;
                    case InlineFile i:
                        NoWrapText($"* `{i.To}` from `{i.SourceData.Length}` byte file included in modlist");
                        break;
                    case CreateBSA i:
                        NoWrapText($"* `{i.To}` by creating a BSA of files found in `{Consts.BSACreationDir}\\{i.TempID}`");
                        break;
                }
            }
        }

        private IEnumerable<Archive> SortArchives(List<Archive> lstArchives)
        {
            var lst = lstArchives.OfType<NexusMod>().OrderBy(m => m.Author).ThenBy(m => m.Name);
            return lst.Concat(lstArchives.Where(m => !(m is NexusMod)).OrderBy(m => m.Name));
        }

        public void Dispose()
        {
            wtr.Flush();
            wtr?.Dispose();
        }
    }
}
