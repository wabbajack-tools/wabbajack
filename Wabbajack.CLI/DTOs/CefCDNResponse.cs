using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.RuntimeInformation;

namespace Wabbajack.CLI.DTOs
{
// Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
    public class File
    {
        [JsonPropertyName("last_modified")] public DateTime LastModified { get; set; } = default!;

        [JsonPropertyName("name")] public string Name { get; set; } = "";

        [JsonPropertyName("sha1")] public string Sha1 { get; set; } = "";

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("type")] public string Type { get; set; } = "";
    }

    public class Version
    {
        [JsonPropertyName("cef_version")] public string CefVersion { get; set; } = "";

        [JsonPropertyName("channel")] public string Channel { get; set; } = "";

        [JsonPropertyName("chromium_version")] public string ChromiumVersion { get; set; } = "";

        [JsonPropertyName("files")]
        public List<File> Files { get; set; } = new();
    }

    public class Linux32
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; }  = new();
    }

    public class Linux64
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; } = new();
    }

    public class Linuxarm
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; } = new();
    }

    public class Linuxarm64
    {
        [JsonPropertyName("versions")] public List<Version> Versions { get; set; } = new();
    }

    public class Macosarm64
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; } = new();
    }

    public class Macosx64
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; }  = new();
    }

    public class Windows32
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; }  = new();
    }

    public class Windows64
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; }  = new();
    }

    public class Windowsarm64
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; }  = new();
    }

    public class CefCDNResponse
    {
        [JsonPropertyName("linux32")]
        public Linux32 Linux32 { get; set; }  = new();

        [JsonPropertyName("linux64")]
        public Linux64 Linux64 { get; set; }  = new();

        [JsonPropertyName("linuxarm")]
        public Linuxarm Linuxarm { get; set; }  = new();

        [JsonPropertyName("linuxarm64")]
        public Linuxarm64 Linuxarm64 { get; set; }  = new();

        [JsonPropertyName("macosarm64")]
        public Macosarm64 Macosarm64 { get; set; }  = new();

        [JsonPropertyName("macosx64")]
        public Macosx64 Macosx64 { get; set; }  = new();

        [JsonPropertyName("windows32")]
        public Windows32 Windows32 { get; set; }  = new();

        [JsonPropertyName("windows64")]
        public Windows64 Windows64 { get; set; }  = new();

        [JsonPropertyName("windowsarm64")]
        public Windowsarm64 Windowsarm64 { get; set; }  = new();

        public static async Task<CefCDNResponse> Load(HttpClient client)
        {
            return (await client.GetFromJsonAsync<CefCDNResponse>("https://cef-builds.spotifycdn.com/index.json"))!;

        }

        public File FindSource(string downloadVersion)
        {
            string os = "";
            if (IsOSPlatform(OSPlatform.Linux))
                os = "Linux";
            if (IsOSPlatform(OSPlatform.Windows))
                os = "Windows";
            if (IsOSPlatform(OSPlatform.OSX))
                os = "OSX";

            var tuple = (os, ProcessArchitecture);
            
            List<Version> versions = new();
            
            if (tuple == ("Linux", Architecture.X64)) versions = Linux64.Versions;
            else if (tuple == ("Linux", Architecture.X86)) versions = Linux32.Versions;
            else if (tuple == ("Windows", Architecture.X64)) versions = Windows64.Versions;
            else if (tuple == ("OSX", Architecture.X64)) versions = Macosx64.Versions;

            var version = versions.Where(v => v.CefVersion.StartsWith(downloadVersion + "."))
                .OrderByDescending(v => v.ChromiumVersion)
                .First();
            return version.Files.First(f => f.Type == "client");
        }
    }


}