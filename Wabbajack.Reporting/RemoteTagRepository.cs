namespace Wabbajack.Reporting;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed class RemoteTagRepository
{
    private readonly string _rawBase; // https://raw.githubusercontent.com/JanuarySnow/WJ-Bot/main/
    private static readonly HttpClient _http = new HttpClient();

    public IReadOnlyDictionary<string, TagConfig> Tags { get; private set; } = new Dictionary<string, TagConfig>();

    private RemoteTagRepository(string rawBase) => _rawBase = rawBase.TrimEnd('/') + "/";

    public static RemoteTagRepository LoadFromUrl(string yamlUrl, string rawBase)
        => LoadFromUrlAsync(yamlUrl, rawBase).GetAwaiter().GetResult();

    public static async Task<RemoteTagRepository> LoadFromUrlAsync(string yamlUrl, string rawBase)
    {
        var repo = new RemoteTagRepository(rawBase);
        var yaml = await _http.GetStringAsync(yamlUrl).ConfigureAwait(false);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var map = deserializer.Deserialize<Dictionary<string, TagConfig>>(yaml)
                  ?? new Dictionary<string, TagConfig>();

        foreach (var kv in map)
        {
            var name = kv.Key ?? string.Empty;
            var cfg = kv.Value ?? new TagConfig();

            cfg.Name = name;
            cfg.Text = NormalizePath(cfg.Text);
            cfg.Image = NormalizePath(cfg.Image);
            cfg.ImageUrl = NormalizeUrlOrNull(cfg.ImageUrl);
            cfg.Pattern = BuildRegex(cfg.Prompt);
            cfg.LogPattern = BuildRegex(cfg.LogPrompt);
        }

        repo.Tags = map;
        return repo;

        string? NormalizePath(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return null;
            if (IsHttp(p)) return p;

            var rel = p.TrimStart('/');
            return repo._rawBase + rel.Replace('\\', '/');
        }

        static string? NormalizeUrlOrNull(string? u)
            => string.IsNullOrWhiteSpace(u) ? null : (IsHttp(u) ? u : "https://raw.githubusercontent.com/JanuarySnow/WJ-Bot/main/" + u.TrimStart('/'));

        static bool IsHttp(string s) => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                        s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        static Regex? BuildRegex(IEnumerable<string>? items)
        {
            if (items is null) return null;
            var list = new List<string>();
            foreach (var s in items)
                if (!string.IsNullOrWhiteSpace(s)) list.Add(Regex.Escape(s));
            if (list.Count == 0) return null;
            return new Regex(string.Join("|", list), RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}