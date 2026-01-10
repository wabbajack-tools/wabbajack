using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Net.Http;
using Wabbajack.Reporting;

namespace Wabbajack.Reporting;

public sealed record DiagnosticResult(string Title, string Body, string? ImagePathOrUrl)
{
    public static DiagnosticResult None => new("", "", null);
    public bool HasValue => !string.IsNullOrWhiteSpace(Title);
}

public interface ILogDiagnosticService
{
    DiagnosticResult Analyze(string logText);
    DiagnosticResult AnalyzeFile(string path);
}

public sealed class LogDiagnosticService : ILogDiagnosticService
{
    private readonly IReadOnlyDictionary<string, TagConfig> _tags;
    private readonly string _baseDir;
    private static readonly HttpClient _http = new HttpClient();

    public LogDiagnosticService(RemoteTagRepository repo, string? baseDir = null)
        : this(repo.Tags, baseDir) { }

    private LogDiagnosticService(IReadOnlyDictionary<string, TagConfig> tags, string? baseDir)
    {
        _tags = tags;
        _baseDir = baseDir ?? AppContext.BaseDirectory;
    }

    public DiagnosticResult AnalyzeFile(string path)
        => Analyze(File.Exists(path) ? File.ReadAllText(path) : string.Empty);

    public DiagnosticResult Analyze(string logText)
    {
        if (string.IsNullOrEmpty(logText)) return DiagnosticResult.None;

        var matches = new List<(TagConfig Tag, int Index)>();
        foreach (var cfg in _tags.Values)
        {
            if (!cfg.Log) continue;
            var patt = cfg.LogPattern ?? cfg.Pattern;
            if (patt is null) continue;

            var m = patt.Matches(logText);
            if (m.Count > 0) matches.Add((cfg, m[m.Count - 1].Index));
        }
        if (matches.Count == 0) return DiagnosticResult.None;

        var names = matches.Select(m => m.Tag.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Contains("curios") && names.Contains("aecc"))
            return Build(matches.First(m => m.Tag.Name.Equals("aecc", StringComparison.OrdinalIgnoreCase)).Tag);
        if (names.Contains("notenoughspace") && names.Contains("path_not_found"))
            return Build(matches.First(m => m.Tag.Name.Equals("notenoughspace", StringComparison.OrdinalIgnoreCase)).Tag);

        return Build(matches.MaxBy(m => m.Index).Tag);
    }

    private DiagnosticResult Build(TagConfig cfg)
    {
        var body = ReadText(cfg.Text) ?? "(snippet missing)";
        var img = !string.IsNullOrWhiteSpace(cfg.ImageUrl) ? cfg.ImageUrl
                 : !string.IsNullOrWhiteSpace(cfg.Image) ? Resolve(cfg.Image!)
                 : null;

        return new DiagnosticResult(
            Title: cfg.Name.Replace('_', ' '),
            Body: body,
            ImagePathOrUrl: img
        );
    }

    private string? ReadText(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return null;
        var p = Resolve(pathOrUrl);
        try
        {
            if (IsHttp(p)) return _http.GetStringAsync(p).GetAwaiter().GetResult();
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }
        catch
        {
            return null;
        }
    }

    private string Resolve(string relOrAbs)
    {
        if (IsHttp(relOrAbs)) return relOrAbs;
        var fixedSep = relOrAbs.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_baseDir, fixedSep.TrimStart(Path.DirectorySeparatorChar));
    }

    private static bool IsHttp(string s) =>
        s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
