using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.NexusApi;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack
{
    public class ReportBuilder : IDisposable
    {
        private const int WRAP_SIZE = 80;
        private readonly StreamWriter wtr;
        private string _output_folder;

        public ReportBuilder(Stream str, string output_folder)
        {
            _output_folder = output_folder;
            wtr = new StreamWriter(str);
        }

        public void Dispose()
        {
            wtr.Flush();
            wtr?.Dispose();
        }

        public void Text(string txt)
        {
            var offset = 0;
            while (offset + WRAP_SIZE < txt.Length)
            {
                wtr.WriteLine(txt.Substring(offset, WRAP_SIZE));
                offset += WRAP_SIZE;
            }

            if (offset < txt.Length) wtr.WriteLine(txt.Substring(offset, txt.Length - offset));
        }

        public void NoWrapText(string txt)
        {
            wtr.WriteLine(txt);
        }

        public void Build(ModList lst)
        {
            Text($"### {lst.Name} - Installation Summary");
            Text(
                $"#### Download Summary ({lst.Archives.Count} archives - {lst.Archives.Sum(a => a.Size).ToFileSizeString()})");
            foreach (var archive in SortArchives(lst.Archives))
            {
                var hash = archive.Hash.FromBase64().ToHEX();
                switch (archive)
                {
                    case NexusMod m:
                        var profile = m.UploaderProfile.Replace("/games/",
                            "/" + NexusApiUtils.ConvertGameName(m.GameName).ToLower() + "/");
                        NoWrapText(
                            $"* [{m.Name}](http://nexusmods.com/{NexusApiUtils.ConvertGameName(m.GameName)}/mods/{m.ModID})");
                        NoWrapText($"    * Author : [{m.UploadedBy}]({profile})");
                        NoWrapText($"    * Version : {m.Version}");
                        break;
                    case MODDBArchive m:
                        NoWrapText($"* MODDB - [{m.Name}]({m.URL})");
                        break;
                    case MEGAArchive m:
                        NoWrapText($"* MEGA - [{m.Name}]({m.URL})");
                        break;
                    case GoogleDriveMod m:
                        NoWrapText(
                            $"* GoogleDrive - [{m.Name}](https://drive.google.com/uc?id={m.Id}&export=download)");
                        break;
                    case DirectURLArchive m:
                        NoWrapText($"* URL - [{m.Name} - {m.URL}]({m.URL})");
                        break;
                }

                NoWrapText($"    * Size : {archive.Size.ToFileSizeString()}");
                NoWrapText($"    * SHA256 : [{hash}](https://www.virustotal.com/gui/file/{hash})");
            }

            Text("\n\n");
            var patched = lst.Directives.OfType<PatchedFromArchive>().OrderBy(p => p.To).ToList();
            Text($"#### Summary of ({patched.Count}) patches");
            foreach (var directive in patched)
                NoWrapText(
                    $"* Applying {SizeForID(directive.PatchID)} byte patch `{directive.FullPath}` to create `{directive.To}`");


            var files = lst.Directives.OrderBy(d => d.To).ToList();
            Text($"\n\n### Install Plan of ({files.Count}) files");
            Text("(ignoring files that are directly copied from archives or listed in the patches section above)");
            foreach (var directive in files.OrderBy(f => f.GetType().Name).ThenByDescending(f => f.To))
                switch (directive)
                {
                    case FromArchive f:
                        //NoWrapText($"* `{f.To}` from `{f.FullPath}`");
                        break;
                    case CleanedESM i:
                        NoWrapText($"* `{i.To}` by applying a patch to a game ESM ({i.SourceESMHash})");
                        break;
                    case RemappedInlineFile i:
                        NoWrapText($"* `{i.To}` by remapping the contents of an inline file");
                        break;
                    case InlineFile i:
                        NoWrapText($"* `{i.To}` from `{SizeForID(i.SourceDataID).ToFileSizeString()}` file included in modlist");
                        break;
                    case CreateBSA i:
                        NoWrapText(
                            $"* `{i.To}` by creating a BSA of files found in `{Consts.BSACreationDir}\\{i.TempID}`");
                        break;
                }

            var inlined = lst.Directives.OfType<InlineFile>()
                .Select(f => (f.To, "inlined", SizeForID(f.SourceDataID)))
                .Concat(lst.Directives
                    .OfType<PatchedFromArchive>()
                    .Select(f => (f.To, "patched", SizeForID(f.PatchID))))
                .ToHashSet()
                .OrderByDescending(f => f.Item3);

            NoWrapText("\n\n### Summary of inlined files in this installer");
            foreach (var inline in inlined)
            {
                NoWrapText($"* {inline.Item3.ToFileSizeString()} for {inline.Item2} file {inline.To}");
            }
        }

        private long SizeForID(string id)
        {
            return File.GetSize(Path.Combine(_output_folder, id));
        }

        private IEnumerable<Archive> SortArchives(List<Archive> lstArchives)
        {
            var lst = lstArchives.OfType<NexusMod>().OrderBy(m => m.Author).ThenBy(m => m.Name);
            return lst.Concat(lstArchives.Where(m => !(m is NexusMod)).OrderBy(m => m.Name));
        }
    }
}