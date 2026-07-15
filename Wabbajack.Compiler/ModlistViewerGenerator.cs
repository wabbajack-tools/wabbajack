using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler;

public class ModlistViewerInfo
{
    public string Title { get; set; } = "";
    public string Game { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
}

public static class ModlistViewerGenerator
{
    private static readonly string[] CandidateFiles =
    {
        "loadorder.txt", "modlist.txt", "plugins.txt",
        "skyrim.ini", "skyrimvr.ini", "skyrimprefs.ini", "skyrimcustom.ini",
        "fallout4.ini", "fallout4prefs.ini", "fallout4custom.ini",
        "settings.ini", "initweaks.ini", "archives.txt"
    };

    public static async Task<string> GenerateFromProfileAsync(AbsolutePath profileFolder, AbsolutePath modsFolder, ModlistViewerInfo info, string? slug = null)
    {
        var files = new List<(string Name, string Content)>();
        foreach (var name in CandidateFiles)
        {
            var path = profileFolder.Combine(name);
            if (path.FileExists())
                files.Add((name, await path.ReadAllTextAsync()));
        }

        var modUrls = new Dictionary<string, string>(StringComparer.Ordinal);
        var modlistEntry = files.FirstOrDefault(f => f.Name.Equals("modlist.txt", StringComparison.OrdinalIgnoreCase));
        if (modlistEntry.Content != null)
        {
            foreach (var group in ParseModlist(modlistEntry.Content))
            foreach (var item in group.Items)
            {
                if (modUrls.ContainsKey(item.Label)) continue;
                var meta = modsFolder.Combine(item.Label, "meta.ini");
                if (!meta.FileExists()) continue;
                var url = ResolveMetaUrl(await meta.ReadAllTextAsync());
                if (url != null) modUrls[item.Label] = url;
            }
        }

        return Build(info, files, modUrls, slug);
    }

    public static string Build(ModlistViewerInfo info, IReadOnlyList<(string Name, string Content)> files,
        IReadOnlyDictionary<string, string>? modUrls = null, string? slug = null)
    {
        int? totalMods = null, enabledMods = null, enabledPlugins = null;

        var modlist = files.FirstOrDefault(f => f.Name.Equals("modlist.txt", StringComparison.OrdinalIgnoreCase));
        if (modlist.Content != null)
        {
            var groups = ParseModlist(modlist.Content);
            totalMods = groups.Sum(g => g.Items.Count);
            enabledMods = groups.Sum(g => g.Items.Count(i => i.Enabled));
        }

        var plugins = files.FirstOrDefault(f => f.Name.Equals("plugins.txt", StringComparison.OrdinalIgnoreCase));
        if (plugins.Content != null)
            enabledPlugins = ParsePlugins(plugins.Content).Count(p => p.Enabled);

        var cards = new StringBuilder();
        foreach (var (name, content) in files)
            cards.Append(FileCard(name, content, modUrls));

        var badge = string.IsNullOrWhiteSpace(info.Version) ? "" : $"<span class='badge'>{Enc(info.Version)}</span>";
        var game = string.IsNullOrWhiteSpace(info.Game) ? "" : $"<div class='game'>{Enc(info.Game)}</div>";
        var gameRow = string.IsNullOrWhiteSpace(info.Game)
            ? ""
            : $"<div class='info-row'><span>Game</span><span class='v'>{Enc(info.Game)}</span></div>";
        var desc = string.IsNullOrWhiteSpace(info.Description) ? "No description provided." : Enc(info.Description);
        var author = string.IsNullOrWhiteSpace(info.Author) ? "Anonymous" : Enc(info.Author);

        var stats = new StringBuilder();
        stats.Append(Stat(enabledMods, "Mods enabled"));
        stats.Append(Stat(totalMods, "Mods total"));
        stats.Append(Stat(enabledPlugins, "Plugins active"));

        var title = Enc(info.Title);

        return $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>{title} - Modlist</title>
<style>{Style}</style>
</head>
<body data-slug='{Enc(slug ?? "")}'>
<div class='layout'>
  <aside class='sidebar'>
    <div class='brand'>
      <div class='logo'>M</div>
      <div class='name'>Modlist Viewer</div>
    </div>
    <nav class='nav'>
      <a class='active' href='#'>List</a>
      <div id='list-nav' hidden>
        <a href='../index.html'>All lists</a>
        <select id='list-switcher' class='list-switcher'></select>
      </div>
    </nav>
  </aside>
  <main class='main'>
    <div class='content-grid'>
      <div>
        <div class='panel header-card'>
          <div class='header-top'>
            <div>
              <h1>{title} {badge}</h1>
              {game}
              <div class='author'>by <b>{author}</b></div>
            </div>
          </div>
          <div class='desc'>{desc}</div>
        </div>
        {cards}
      </div>
      <div>
        <div class='panel info-card'>
          <h3>Information</h3>
          {gameRow}
          <div class='info-row'><span>Files</span><span class='v'>{files.Count}</span></div>
          <div class='stat-grid'>{stats}</div>
        </div>
      </div>
    </div>
  </main>
</div>
<script>{Script}</script>
</body>
</html>";
    }

    public static string Sanitize(string name)
        => new string(name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '-').ToArray());

    public static string BuildHub(IReadOnlyList<ModlistViewerListEntry> entries)
    {
        var ordered = entries.OrderByDescending(e => e.Updated).ToList();

        if (ordered.Count == 1)
        {
            var only = Sanitize(ordered[0].Slug);
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta http-equiv='refresh' content='0; url=lists/{only}.html'>
<title>{Enc(ordered[0].Name)}</title>
</head>
<body>
<p>Redirecting to <a href='lists/{only}.html'>{Enc(ordered[0].Name)}</a></p>
</body>
</html>";
        }

        var cards = new StringBuilder();
        foreach (var e in ordered)
        {
            var s = Sanitize(e.Slug);
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(e.Game)) meta.Add(Enc(e.Game));
            if (!string.IsNullOrWhiteSpace(e.Version)) meta.Add("v" + Enc(e.Version));
            meta.Add("updated " + e.Updated.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cards.Append($"<a class='hub-card' href='lists/{s}.html'>");
            cards.Append($"<div class='hub-name'>{Enc(e.Name)}</div>");
            cards.Append($"<div class='hub-meta'>{string.Join(" &middot; ", meta)}</div>");
            cards.Append("</a>");
        }

        return $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>Modlists</title>
<style>{Style}</style>
</head>
<body>
<div class='layout'>
  <aside class='sidebar'>
    <div class='brand'>
      <div class='logo'>M</div>
      <div class='name'>Modlist Viewer</div>
    </div>
    <nav class='nav'>
      <a class='active' href='#'>Modlists</a>
    </nav>
  </aside>
  <main class='main'>
    <div class='panel header-card'>
      <h1>Modlists</h1>
      <div class='desc'>{ordered.Count} lists in this repository.</div>
    </div>
    <div class='hub-list'>{cards}</div>
  </main>
</div>
</body>
</html>";
    }

    private record ModItem(string Label, bool Enabled, bool Managed);

    private record ModGroup(string? Name)
    {
        public List<ModItem> Items { get; } = new();
    }

    private record ListItem(string Label, bool Enabled);

    private static IEnumerable<string> SplitLines(string text)
    {
        foreach (var line in text.Replace("\r", "").Split('\n'))
            if (line.Length > 0 && line[0] != '#')
                yield return line;
    }

    private static List<ModGroup> ParseModlist(string text)
    {
        var groups = new List<ModGroup>();
        var current = new ModGroup(null);
        groups.Add(current);

        foreach (var raw in SplitLines(text).Reverse())
        {
            var flag = raw[0];
            var name = raw.Substring(1);
            var enabled = flag == '+' || flag == '*';
            var managed = flag == '*';

            if (name.EndsWith("_separator"))
            {
                var label = name.Substring(0, name.Length - "_separator".Length).Trim();
                if (label.Length == 0) label = "-";
                current = new ModGroup(label);
                groups.Add(current);
            }
            else
            {
                current.Items.Add(new ModItem(name, enabled, managed));
            }
        }

        return groups.Where(g => g.Items.Count > 0 || g.Name != null).ToList();
    }

    private static List<ListItem> ParsePlugins(string text)
    {
        var items = new List<ListItem>();
        foreach (var raw in SplitLines(text))
        {
            var enabled = raw.StartsWith("*");
            items.Add(new ListItem(enabled ? raw.Substring(1) : raw, enabled));
        }
        return items;
    }

    private static List<ListItem> ParseLoadorder(string text)
        => SplitLines(text).Select(l => new ListItem(l, true)).ToList();

    private static string Kind(string name)
    {
        var n = name.ToLowerInvariant();
        if (n == "modlist.txt") return "modlist";
        if (n == "plugins.txt") return "plugins";
        if (n == "loadorder.txt") return "loadorder";
        return "raw";
    }

    private static string FileCard(string name, string content, IReadOnlyDictionary<string, string>? modUrls)
    {
        var cid = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var sb = new StringBuilder();
        sb.Append($"<div class='panel file-card collapsed' data-file='{Enc(name)}'>");
        sb.Append("<div class='file-head' data-toggle>");
        sb.Append("<span class='file-icon'>[txt]</span>");
        sb.Append("<div class='file-meta'>");
        sb.Append($"<div class='file-name'>{Enc(name)}</div>");
        sb.Append($"<div class='file-sub'>{Subtitle(name, content)}</div>");
        sb.Append("</div>");
        sb.Append("<div class='file-actions'><span class='chevron'>&#9662;</span></div>");
        sb.Append("</div>");
        sb.Append($"<div class='file-body' id='body-{cid}'>");
        sb.Append("<div class='search-wrap'><input type='search' placeholder='Search content...' data-search></div>");
        sb.Append("<div class='rows' data-rows>");
        sb.Append(Body(name, content, modUrls));
        sb.Append("</div>");
        sb.Append("<div class='no-match' data-empty hidden>No matches.</div>");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string Subtitle(string name, string content)
    {
        var size = HumanSize(Encoding.UTF8.GetByteCount(content));
        switch (Kind(name))
        {
            case "modlist":
                var groups = ParseModlist(content);
                var total = groups.Sum(g => g.Items.Count);
                var on = groups.Sum(g => g.Items.Count(i => i.Enabled));
                return $"{size} &middot; {on}/{total} mods enabled";
            case "plugins":
                var items = ParsePlugins(content);
                return $"{size} &middot; {items.Count(i => i.Enabled)}/{items.Count} plugins active";
            case "loadorder":
                return $"{size} &middot; {SplitLines(content).Count()} entries";
            default:
                return size;
        }
    }

    private static string Body(string name, string content, IReadOnlyDictionary<string, string>? modUrls)
    {
        switch (Kind(name))
        {
            case "modlist": return RenderModlist(ParseModlist(content), modUrls);
            case "plugins": return RenderList(ParsePlugins(content), true);
            case "loadorder": return RenderList(ParseLoadorder(content), false);
            default: return RenderRaw(content);
        }
    }

    private static string RenderModlist(List<ModGroup> groups, IReadOnlyDictionary<string, string>? modUrls)
    {
        var sb = new StringBuilder();
        var idx = 0;
        foreach (var g in groups)
        {
            sb.Append("<div class='group'>");
            if (g.Name != null)
            {
                var n = g.Items.Count;
                sb.Append($"<div class='sep-head' data-text='{Enc(g.Name.ToLowerInvariant())}'>");
                sb.Append($"<span>{Enc(g.Name)}</span>");
                sb.Append($"<span class='count'>{n} item{(n == 1 ? "" : "s")}</span></div>");
            }
            foreach (var it in g.Items)
            {
                idx++;
                string? url = null;
                modUrls?.TryGetValue(it.Label, out url);
                AppendRow(sb, idx, it.Label, it.Enabled, it.Managed ? "dlc" : null, false, url);
            }
            sb.Append("</div>");
        }
        return sb.ToString();
    }

    private static string RenderList(List<ListItem> items, bool showFlag)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='group'>");
        for (var i = 0; i < items.Count; i++)
            AppendRow(sb, i + 1, items[i].Label, items[i].Enabled, null, showFlag, null);
        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int index, string label, bool enabled, string? flagType, bool showFlag, string? url)
    {
        var flag = "";
        if (flagType == "dlc") flag = "<span class='flag dlc'>DLC</span>";
        else if (showFlag) flag = enabled ? "<span class='flag on'>ON</span>" : "<span class='flag off'>OFF</span>";
        var cls = enabled ? "row" : "row disabled";
        var labelHtml = url == null
            ? $"<span class='label'>{Enc(label)}</span>"
            : $"<a class='label' href='{Enc(url)}' target='_blank' rel='noopener'>{Enc(label)}</a>";
        sb.Append($"<div class='{cls}' data-text='{Enc(label.ToLowerInvariant())}'>");
        sb.Append($"<div class='row-index'>{index}</div>");
        sb.Append($"<div class='row-body'>{flag}{labelHtml}</div>");
        sb.Append("</div>");
    }

    private static string? ResolveMetaUrl(string meta)
    {
        string? url = null, gameName = null, modid = null;
        var custom = false;
        foreach (var line in meta.Replace("\r", "").Split('\n'))
        {
            if (line.StartsWith("url=")) url = line.Substring(4).Trim();
            else if (line.StartsWith("gameName=")) gameName = line.Substring(9).Trim();
            else if (line.StartsWith("modid=")) modid = line.Substring(6).Trim();
            else if (line.StartsWith("hasCustomURL=")) custom = line.Substring(13).Trim() == "true";
        }

        if (custom && IsHttp(url)) return url;

        var domain = NexusDomain(gameName);
        if (domain != null && int.TryParse(modid, out var id) && id > 0)
            return $"https://www.nexusmods.com/{domain}/mods/{id}";

        return IsHttp(url) ? url : null;
    }

    private static bool IsHttp(string? s) => s != null && (s.StartsWith("http://") || s.StartsWith("https://"));

    private static string? NexusDomain(string? gameName)
    {
        switch (gameName?.ToLowerInvariant())
        {
            case "skyrimse":
            case "skyrimspecialedition":
            case "skyrimvr": return "skyrimspecialedition";
            case "skyrim": return "skyrim";
            case "enderal": return "enderal";
            case "enderalse": return "enderalspecialedition";
            case "fallout4":
            case "fallout4vr": return "fallout4";
            case "fallout3": return "fallout3";
            case "falloutnv":
            case "newvegas": return "newvegas";
            case "oblivion": return "oblivion";
            case "morrowind": return "morrowind";
            case "starfield": return "starfield";
            default: return null;
        }
    }

    private static string RenderRaw(string content)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='group raw'>");
        foreach (var line in content.Replace("\r", "").Split('\n'))
        {
            var stripped = line.Trim();
            string inner;
            if (stripped.StartsWith("[") && stripped.EndsWith("]"))
            {
                inner = $"<span class='ini-section'>{Enc(line)}</span>";
            }
            else if (line.Contains("="))
            {
                var eq = line.IndexOf('=');
                inner = $"<span class='ini-key'>{Enc(line.Substring(0, eq))}</span>={Enc(line.Substring(eq + 1))}";
            }
            else
            {
                inner = line.Length == 0 ? "&nbsp;" : Enc(line);
            }
            sb.Append($"<div class='raw-line' data-text='{Enc(line.ToLowerInvariant())}'>{inner}</div>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Stat(int? num, string label)
        => num == null ? "" : $"<div class='stat'><div class='num'>{num.Value:N0}</div><div class='lbl'>{label}</div></div>";

    private static string HumanSize(long n)
    {
        if (n < 1024) return $"{n} B";
        if (n < 1024 * 1024) return $"{n / 1024.0:0.00} KiB";
        return $"{n / 1024.0 / 1024.0:0.00} MiB";
    }

    private static string Enc(string s) => WebUtility.HtmlEncode(s ?? "");

    private const string Script = @"
document.querySelectorAll('.file-card').forEach(function (card) {
  var head = card.querySelector('[data-toggle]');
  head.addEventListener('click', function (e) {
    if (e.target.closest('.dl')) return;
    card.classList.toggle('collapsed');
  });
  var input = card.querySelector('[data-search]');
  var groups = card.querySelectorAll('.group');
  var empty = card.querySelector('[data-empty]');
  input.addEventListener('input', function () {
    var q = input.value.trim().toLowerCase();
    var anyVisible = false;
    groups.forEach(function (group) {
      var header = group.querySelector('.sep-head');
      var groupVisible = false;
      group.querySelectorAll('[data-text]').forEach(function (row) {
        if (row === header) return;
        var match = !q || row.getAttribute('data-text').indexOf(q) !== -1;
        row.hidden = !match;
        if (match) groupVisible = true;
      });
      if (header) {
        var headerMatch = q && header.getAttribute('data-text').indexOf(q) !== -1;
        if (headerMatch) {
          group.querySelectorAll('[data-text]').forEach(function (row) { row.hidden = false; });
          groupVisible = true;
        }
        header.hidden = false;
      }
      group.hidden = !groupVisible;
      if (groupVisible) anyVisible = true;
    });
    if (empty) empty.hidden = anyVisible;
  });
});
(function () {
  var sw = document.getElementById('list-switcher');
  var nav = document.getElementById('list-nav');
  if (!sw || !nav) return;
  var current = document.body.getAttribute('data-slug');
  fetch('../lists.json').then(function (r) { return r.ok ? r.json() : null; }).then(function (lists) {
    if (!lists || lists.length < 2) return;
    lists.forEach(function (e) {
      var o = document.createElement('option');
      o.value = e.slug; o.textContent = e.name;
      if (e.slug === current) o.selected = true;
      sw.appendChild(o);
    });
    nav.hidden = false;
    sw.addEventListener('change', function () { window.location.href = sw.value + '.html'; });
  }).catch(function () {});
})();
";

    private const string Style = @"
:root{--bg:#161922;--panel:#1e2230;--panel-2:#252a3a;--row:#232838;--row-alt:#1f2433;--border:#2e3446;--text:#d6dbe7;--muted:#8b93a7;--accent:#8bcf5f;--accent-dim:#4f7a2e;--link:#88a6dd;--index:#2c4a63;--danger:#d8654f;}
*{box-sizing:border-box;}
[hidden]{display:none !important;}
html,body{margin:0;padding:0;background:var(--bg);color:var(--text);font-family:'Segoe UI',Inter,system-ui,sans-serif;font-size:15px;line-height:1.4;}
a{color:var(--link);text-decoration:none;}
a:hover{text-decoration:underline;}
.layout{display:flex;min-height:100vh;}
.sidebar{width:230px;flex-shrink:0;background:var(--bg);border-right:1px solid var(--border);padding:24px 16px;position:sticky;top:0;height:100vh;}
.brand{text-align:center;margin-bottom:28px;}
.brand .logo{font-size:44px;font-weight:800;color:var(--accent);letter-spacing:2px;line-height:1;}
.brand .name{color:var(--accent);font-weight:700;margin-top:6px;}
.nav a{display:flex;align-items:center;gap:10px;padding:10px 14px;border-radius:8px;color:var(--text);margin-bottom:4px;font-weight:500;}
.nav a:hover{background:var(--panel);text-decoration:none;}
.nav a.active{background:var(--accent-dim);color:#fff;}
#list-nav{margin-top:14px;padding-top:14px;border-top:1px solid var(--border);}
#list-nav a{display:block;color:var(--muted);padding:6px 14px;font-size:13px;}
#list-nav a:hover{color:var(--text);text-decoration:none;}
.list-switcher{width:100%;margin-top:8px;background:var(--bg);color:var(--text);border:1px solid var(--border);border-radius:8px;padding:9px 10px;font-size:14px;}
.list-switcher:focus{outline:none;border-color:var(--accent-dim);}
.hub-list{display:flex;flex-direction:column;gap:12px;}
.hub-card{display:block;background:var(--panel);border:1px solid var(--border);border-radius:12px;padding:18px 22px;}
.hub-card:hover{border-color:var(--accent-dim);background:var(--panel-2);text-decoration:none;}
.hub-name{color:var(--accent);font-weight:700;font-size:18px;}
.hub-meta{color:var(--muted);font-size:13px;margin-top:4px;}
.main{flex:1;padding:28px 36px;max-width:1500px;}
.content-grid{display:grid;grid-template-columns:1fr 320px;gap:24px;align-items:start;}
@media(max-width:1100px){.content-grid{grid-template-columns:1fr;}.sidebar{display:none;}}
.panel{background:var(--panel);border:1px solid var(--border);border-radius:12px;margin-bottom:22px;overflow:hidden;}
.header-card{padding:28px 30px;}
.header-top{display:flex;justify-content:space-between;align-items:flex-start;gap:16px;flex-wrap:wrap;}
.header-card h1{margin:0;font-size:30px;color:var(--accent);display:inline-flex;align-items:center;gap:12px;}
.badge{background:var(--panel-2);color:var(--link);border:1px solid var(--border);font-size:13px;padding:3px 10px;border-radius:6px;font-weight:600;}
.header-card .game{color:var(--link);font-weight:600;margin-top:10px;}
.header-card .author{color:var(--muted);margin-top:6px;}
.header-card .author b{color:var(--accent);}
.header-card .desc{color:var(--muted);margin-top:22px;}
.file-card .file-head{display:flex;align-items:center;gap:12px;padding:18px 22px;cursor:pointer;user-select:none;}
.file-card .file-head:hover{background:var(--panel-2);}
.file-icon{color:var(--muted);font-size:12px;font-family:'Cascadia Code',Consolas,monospace;}
.file-meta{flex:1;}
.file-name{color:var(--accent);font-weight:700;font-size:16px;}
.file-sub{color:var(--muted);font-size:13px;margin-top:2px;}
.file-actions{display:flex;align-items:center;gap:14px;color:var(--muted);}
.chevron{transition:transform .15s;}
.file-card.collapsed .chevron{transform:rotate(-90deg);}
.file-card.collapsed .file-body{display:none;}
.search-wrap{padding:14px 18px;border-top:1px solid var(--border);}
.search-wrap input{width:100%;background:var(--bg);border:1px solid var(--border);color:var(--text);padding:11px 14px;border-radius:8px;font-size:14px;}
.search-wrap input:focus{outline:none;border-color:var(--accent-dim);}
.rows{border-top:1px solid var(--border);}
.group{display:block;}
.row{display:flex;align-items:stretch;min-height:40px;}
.row:nth-child(odd) .row-body{background:var(--row-alt);}
.row:nth-child(even) .row-body{background:var(--row);}
.row-index{width:56px;flex-shrink:0;background:var(--index);color:#cfe0ef;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:600;}
.row-body{flex:1;display:flex;align-items:center;padding:8px 18px;gap:10px;}
.row.disabled .row-body{opacity:.45;}
.row.disabled .label{text-decoration:line-through;color:var(--muted);}
.flag{font-size:11px;font-weight:700;padding:1px 7px;border-radius:4px;flex-shrink:0;}
.flag.on{background:rgba(139,207,95,.16);color:var(--accent);}
.flag.off{background:rgba(216,101,79,.16);color:var(--danger);}
.flag.dlc{background:rgba(136,166,221,.16);color:var(--link);}
.label{color:var(--link);}
a.label{text-decoration:underline;text-decoration-color:rgba(136,166,221,.35);text-underline-offset:2px;text-decoration-thickness:1px;}
a.label:hover{text-decoration-color:var(--link);}
.sep-head{background:var(--accent-dim);color:#eafbe0;font-weight:700;letter-spacing:.4px;padding:12px 18px;text-transform:uppercase;font-size:13px;display:flex;justify-content:space-between;align-items:center;}
.sep-head .count{font-weight:500;opacity:.85;text-transform:none;}
.raw{background:var(--bg);border-top:1px solid var(--border);padding:8px 0;max-height:600px;overflow:auto;}
.raw-line{font-family:'Cascadia Code',Consolas,monospace;font-size:13px;padding:1px 20px;white-space:pre-wrap;word-break:break-word;}
.ini-section{color:var(--accent);}
.ini-key{color:var(--link);}
.no-match{padding:16px 20px;color:var(--muted);font-style:italic;}
.info-card{padding:22px 24px;}
.info-card h3{margin:0 0 18px;color:var(--link);font-size:18px;}
.info-row{display:flex;align-items:center;gap:10px;color:var(--muted);margin-bottom:14px;}
.info-row .v{margin-left:auto;color:var(--text);}
.stat-grid{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:8px;}
.stat{background:var(--bg);border:1px solid var(--border);border-radius:8px;padding:12px;text-align:center;}
.stat .num{font-size:22px;font-weight:700;color:var(--accent);}
.stat .lbl{font-size:12px;color:var(--muted);margin-top:2px;}
";
}
