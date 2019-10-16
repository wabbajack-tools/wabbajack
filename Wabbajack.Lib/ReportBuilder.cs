using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Lib.NexusApi;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
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

        public void Build(Compiler c, ModList lst)
        {
            Text($"### {lst.Name} by {lst.Author} - Installation Summary");
            Text(lst.Description);
            Text($"#### Website:");
            NoWrapText($"[{lst.Website}]({lst.Website})");

            var readme_file = Path.Combine(c.MO2ProfileDir, "readme.md");
            if (File.Exists(readme_file))
                File.ReadAllLines(readme_file)
                    .Do(NoWrapText);

            Text(
                $"#### Download Summary ({lst.Archives.Count} archives - {lst.Archives.Sum(a => a.Size).ToFileSizeString()})");
            foreach (var archive in SortArchives(lst.Archives))
            {
                var hash = archive.Hash.FromBase64().ToHEX();
                NoWrapText(archive.State.GetReportEntry(archive));
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
                    case PropertyFile i:
                        NoWrapText($"* `{i.SourceDataID}` as a `{Enum.GetName(typeof(PropertyType),i.Type)}`");
                        break;
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
            return lstArchives.OrderByDescending(a => a.Size);
        }
    }
}