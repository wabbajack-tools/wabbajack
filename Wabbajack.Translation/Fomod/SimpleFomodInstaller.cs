using System.Text;
using System.Xml.Linq;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Translation.Fomod;

public sealed record FomodCopy(string Source, string Destination, bool IsFolder);

public static class SimpleFomodInstaller
{
    public static IReadOnlyList<FomodCopy> Plan(string moduleConfig)
    {
        var root = XDocument.Parse(moduleConfig).Root!;
        XNamespace ns = root.Name.Namespace;
        var copies = new List<FomodCopy>();
        AddFiles(root.Element(ns + "requiredInstallFiles"), copies);

        foreach (var group in root.Descendants(ns + "group"))
        {
            var plugins = group.Element(ns + "plugins")?.Elements(ns + "plugin").ToList() ?? [];
            if (plugins.Count == 0) continue;
            var type = group.Attribute("type")?.Value ?? "";
            var chosen = plugins.Where(p => TypeOf(p, ns) is "Required").ToList();
            var recommended = plugins.Where(p => TypeOf(p, ns) is "Recommended").ToList();
            if (type is "SelectAll")
                chosen = plugins;
            else if (recommended.Count > 0)
                chosen.AddRange(recommended);
            else if (type is "SelectExactlyOne" or "SelectAtLeastOne" && chosen.Count == 0)
                chosen.Add(plugins[0]);

            foreach (var plugin in chosen.Distinct())
                AddFiles(plugin.Element(ns + "files"), copies);
        }

        return copies;
    }

    public static async Task<int> Install(AbsolutePath extracted, AbsolutePath output, CancellationToken token)
    {
        var config = extracted.EnumerateFiles(recursive: true)
            .FirstOrDefault(f => f.FileName.ToString().Equals("ModuleConfig.xml", StringComparison.OrdinalIgnoreCase) &&
                                 f.Parent.FileName.ToString().Equals("fomod", StringComparison.OrdinalIgnoreCase));
        if (config == default)
            return await CopyAll(extracted, output, token);

        var baseFolder = config.Parent.Parent;
        var written = 0;
        foreach (var copy in Plan(Decode(await config.ReadAllBytesAsync(token))))
        {
            token.ThrowIfCancellationRequested();
            var source = baseFolder.Combine(copy.Source.Replace('/', '\\'));
            var destination = output.Combine(copy.Destination.Replace('/', '\\'));
            if (copy.IsFolder)
            {
                if (!source.DirectoryExists()) continue;
                foreach (var file in source.EnumerateFiles(recursive: true))
                {
                    var target = destination.Combine(file.RelativeTo(source).ToString());
                    target.Parent.CreateDirectory();
                    await file.CopyToAsync(target, token);
                    written++;
                }
            }
            else if (source.FileExists())
            {
                destination.Parent.CreateDirectory();
                await source.CopyToAsync(destination, token);
                written++;
            }
        }

        return written;
    }

    private static readonly HashSet<string> RootDocuments =
        [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".txt", ".md", ".pdf", ".url", ".html", ".htm", ".rtf", ".doc", ".docx"];

    private static async Task<int> CopyAll(AbsolutePath extracted, AbsolutePath output, CancellationToken token)
    {
        var root = extracted;
        var children = extracted.EnumerateDirectories(recursive: false).ToList();
        var data = children.FirstOrDefault(d => d.FileName.ToString().Equals("Data", StringComparison.OrdinalIgnoreCase));
        if (data != default) root = data;

        var written = 0;
        foreach (var file in root.EnumerateFiles(recursive: true))
        {
            token.ThrowIfCancellationRequested();
            var relative = file.RelativeTo(root);
            if (relative.Depth == 1 && RootDocuments.Contains(file.Extension.ToString().ToLowerInvariant())) continue;
            var target = output.Combine(relative.ToString());
            target.Parent.CreateDirectory();
            await file.CopyToAsync(target, token);
            written++;
        }

        return written;
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && (bytes[0] == 0xFF && bytes[1] == 0xFE || bytes[0] == 0xFE && bytes[1] == 0xFF))
            return Encoding.Unicode.GetString(bytes).TrimStart('﻿');
        return Encoding.UTF8.GetString(bytes).TrimStart('﻿');
    }

    private static string TypeOf(XElement plugin, XNamespace ns) =>
        plugin.Element(ns + "typeDescriptor")?.Element(ns + "type")?.Attribute("name")?.Value ?? "";

    private static void AddFiles(XElement? files, List<FomodCopy> copies)
    {
        if (files == null) return;
        foreach (var element in files.Elements())
        {
            var source = element.Attribute("source")?.Value;
            if (string.IsNullOrWhiteSpace(source)) continue;
            var destination = element.Attribute("destination")?.Value ?? (element.Name.LocalName == "folder" ? "" : source);
            copies.Add(new FomodCopy(source, destination, element.Name.LocalName == "folder"));
        }
    }
}
