using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.Compiler
{
    public static class WabbajackToVortexCollection
    {
        public static string Serialize(ModList modList, string? gameVersion = null)
        {
            var obj = Build(modList, gameVersion);

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(obj, opts);
        }

        public static VortexCollection Build(ModList modList, string? gameVersion = null)
        {
            var listDomain = GetDomain(modList.GameType.ToString());

            var mods = new List<VortexMod>();

            var seenNexus = new HashSet<(string domain, long modId, long fileId)>();
            var seenBrowse = new HashSet<(string domain, string url)>();

            foreach (var arch in modList.Archives ?? Array.Empty<Archive>())
            {
                if (arch?.State == null)
                    continue;

                // Skip game files explicitly
                if (arch.State is GameFileSource)
                    continue;

                var domain = listDomain;

                // Build a VortexSource + basic metadata
                if (!TryBuildVortexModFromArchive(arch, domain, out var vortexMod))
                    continue;

                if (vortexMod.Source.Type == "nexus")
                {
                    if (!seenNexus.Add((domain, vortexMod.Source.ModId, vortexMod.Source.FileId)))
                        continue;
                }
                else if (vortexMod.Source.Type == "browse")
                {
                    var url = vortexMod.Source.Url ?? "";
                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    if (!seenBrowse.Add((domain, url)))
                        continue;
                }

                mods.Add(vortexMod);
            }

            // Use the modlist description for BOTH fields
            var description = modList.Description ?? "";

            // If description is empty, use a default
            if (string.IsNullOrWhiteSpace(description))
            {
                description = "A Wabbajack modlist for " + modList.GameType;
            }

            var gameVersions = !string.IsNullOrWhiteSpace(gameVersion)
                ? new List<string> { gameVersion }
                : new List<string>();

            return new VortexCollection
            {
                Info = new VortexInfo
                {
                    Author = modList.Author ?? "",
                    AuthorUrl = modList.Website ?? "",
                    Name = modList.Name ?? "",
                    Summary = description,
                    Description = description,
                    InstallInstructions = "This collection was created by Wabbajack. Download and install using Wabbajack from: https://www.wabbajack.org/",
                    DomainName = listDomain,
                    GameVersions = gameVersions,
                },
                Mods = mods,
                CollectionConfig = new Dictionary<string, object>(),
                LoadOrder = new List<object>(),
                ModRules = new List<object>(),
                Tools = new List<object>()
            };
        }

        private static bool TryBuildVortexModFromArchive(Archive arch, string domain, out VortexMod mod)
        {
            mod = new VortexMod();

            var fileSize = (long)arch.Size;
            var logicalName = string.IsNullOrWhiteSpace(arch.Name) ? "Unknown" : arch.Name;

            string name = logicalName;
            string version = "";
            string author = "";

            if (arch.State is IMetaState meta)
            {
                if (!string.IsNullOrWhiteSpace(meta.Name)) name = meta.Name!;
                if (!string.IsNullOrWhiteSpace(meta.Version)) version = meta.Version!;
                if (!string.IsNullOrWhiteSpace(meta.Author)) author = meta.Author!;
            }

            // Build source based on download state type
            VortexSource source;

            if (arch.State is Nexus nexusState)
            {
                var modId = (long)nexusState.ModID;
                var fileId = (long)nexusState.FileID;
                if (modId <= 0 || fileId <= 0)
                    return false;

                source = new VortexSource
                {
                    Type = "nexus",
                    ModId = modId,
                    FileId = fileId,
                    LogicalFilename = name,
                    UpdatePolicy = "prefer",
                    FileSize = fileSize,
                };

                // Prefer Nexus state fields if present
                if (!string.IsNullOrWhiteSpace(nexusState.Name)) name = nexusState.Name!;
                if (!string.IsNullOrWhiteSpace(nexusState.Version)) version = nexusState.Version!;
                if (!string.IsNullOrWhiteSpace(nexusState.Author)) author = nexusState.Author!;
            }
            else if (arch.State is Http http)
            {
                if (http.Url == null)
                    return false;

                source = new VortexSource
                {
                    Type = "browse",
                    Url = http.Url.ToString(),
                    // wj uses xxhash, I dont think nm collection *needs* md5 despite it being in the collection json
                    // but if it does can revisit and calculte a hash
                    Md5 = null,
                    LogicalFilename = name,
                    UpdatePolicy = "prefer",
                    FileSize = fileSize,
                };
            }
            else if (arch.State is Manual manual)
            {
                if (manual.Url == null)
                    return false;

                source = new VortexSource
                {
                    Type = "browse",
                    Url = manual.Url.ToString(),
                    Md5 = null,
                    LogicalFilename = name,
                    UpdatePolicy = "prefer",
                    FileSize = fileSize,
                };

            }
            else if (arch.State is GoogleDrive gd)
            {
                var uri = gd.GetUri();

                source = new VortexSource
                {
                    Type = "browse",
                    Url = uri.ToString(),
                    Md5 = null,
                    LogicalFilename = name,
                    UpdatePolicy = "prefer",
                    FileSize = fileSize,
                };
            }
            else
            {
                return false;
            }

            mod = new VortexMod
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name,
                Version = version ?? "",
                Optional = false,
                DomainName = domain,
                Author = author ?? "",
                Details = new VortexDetails { Category = "", Type = "" },
                Phase = 0,
                Source = source
            };

            return true;
        }

        public static string GetDomain(string? someName)
        {
            if (!string.IsNullOrWhiteSpace(someName) &&
                GameRegistry.TryGetByFuzzyName(someName.Trim(), out var meta) &&
                !string.IsNullOrWhiteSpace(meta.NexusName))
            {
                return meta.NexusName!;
            }

            return "site";
        }

        public sealed class VortexCollection
        {
            public VortexInfo Info { get; set; } = new();
            public List<VortexMod> Mods { get; set; } = new();
            public Dictionary<string, object> CollectionConfig { get; set; } = new();
            public List<object> LoadOrder { get; set; } = new();
            public List<object> ModRules { get; set; } = new();
            public List<object> Tools { get; set; } = new();
        }

        public sealed class VortexInfo
        {
            public string Author { get; set; } = "";
            public string AuthorUrl { get; set; } = "";
            public string Name { get; set; } = "";
            public string Summary { get; set; } = "";
            public string Description { get; set; } = "";
            public string InstallInstructions { get; set; } = "";
            public string DomainName { get; set; } = "";
            public List<string> GameVersions { get; set; } = new();
        }

        public sealed class VortexMod
        {
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public bool Optional { get; set; }
            public string DomainName { get; set; } = "";
            public VortexSource Source { get; set; } = new();
            public string Author { get; set; } = "";
            public VortexDetails Details { get; set; } = new();
            public int Phase { get; set; }
        }

        public sealed class VortexSource
        {
            public string Type { get; set; } = "nexus";

            // Nexus-type
            public long ModId { get; set; }
            public long FileId { get; set; }

            public string? Url { get; set; }

            public string? Md5 { get; set; }

            public string LogicalFilename { get; set; } = "";
            public string UpdatePolicy { get; set; } = "prefer";
            public long FileSize { get; set; }
        }

        public sealed class VortexDetails
        {
            public string Category { get; set; } = "";
            public string Type { get; set; } = "";
        }
    }
}