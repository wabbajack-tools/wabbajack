using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Compression.BSA;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public class RawSourceFile
    {
        public readonly RelativePath Path;

        public RawSourceFile(VirtualFile file, RelativePath path)
        {
            File = file;
            Path = path;
        }

        public AbsolutePath AbsolutePath
        {
            get
            {
                if (!File.IsNative)
                    throw new InvalidDataException("Can't get the absolute path of a non-native file");
                return File.FullPath.Base;
            }
        }

        public VirtualFile File { get; }

        public Hash Hash => File.Hash;

        public T EvolveTo<T>() where T : Directive, new()
        {
            var v = new T();
            v.To = Path;
            v.Hash = Hash;
            v.Size = File.Size;
            return v;
        }
    }

    [JsonName("ModList")]
    public class ModList
    {
        /// <summary>
        ///     Archives required by this modlist
        /// </summary>
        public List<Archive> Archives = new List<Archive>();

        /// <summary>
        ///     Author of the ModList
        /// </summary>
        public string Author = string.Empty;

        /// <summary>
        ///     Description of the ModList
        /// </summary>
        public string Description = string.Empty;

        /// <summary>
        ///     Install directives
        /// </summary>
        public List<Directive> Directives = new List<Directive>();

        /// <summary>
        ///     The game variant to which this game applies
        /// </summary>
        public Game GameType;

        /// <summary>
        ///     Hash of the banner-image
        /// </summary>
        public RelativePath Image;

        /// <summary>
        ///     The Mod Manager used to create the modlist
        /// </summary>
        public ModManager ModManager;

        /// <summary>
        ///     Name of the ModList
        /// </summary>
        public string Name = string.Empty;

        /// <summary>
        ///     URL to the readme
        /// </summary>
        public string Readme = string.Empty;

        /// <summary>
        ///     The build version of Wabbajack used when compiling the Modlist
        /// </summary>
        public Version? WabbajackVersion;

        /// <summary>
        ///     Website of the ModList
        /// </summary>
        public Uri? Website;

        /// <summary>
        ///     Current Version of the Modlist
        /// </summary>
        public Version Version = new Version(1, 0, 0, 0);

        /// <summary>
        ///     The size of all the archives once they're downloaded
        /// </summary>
        [JsonIgnore]
        public long DownloadSize => Archives.Sum(a => a.Size);

        /// <summary>
        ///     The size of all the files once they are installed (excluding downloaded archives)
        /// </summary>
        [JsonIgnore]
        public long InstallSize => Directives.Sum(s => s.Size);

        public ModList Clone()
        {
            using var ms = new MemoryStream();
            this.ToJson(ms);
            ms.Position = 0;
            return ms.FromJson<ModList>();
        }
    }

    public abstract class Directive
    {
        public Hash Hash { get; set; }
        public long Size { get; set; }

        /// <summary>
        ///     location the file will be copied to, relative to the install path.
        /// </summary>
        public RelativePath To { get; set; }
    }

    public class IgnoredDirectly : Directive
    {
        public string Reason = string.Empty;
    }

    public class NoMatch : IgnoredDirectly
    {
    }

    [JsonName("InlineFile")]
    public class InlineFile : Directive
    {
        /// <summary>
        ///     Data that will be written as-is to the destination location;
        /// </summary>
        public RelativePath SourceDataID { get; set; }
    }

    [JsonName("ArchiveMeta")]
    public class ArchiveMeta : Directive
    {
        public RelativePath SourceDataID { get; set; }
    }

    public enum PropertyType { Banner, Readme }

    /// <summary>
    ///     File meant to be extracted before the installation
    /// </summary>
    [JsonName("PropertyFile")]
    public class PropertyFile : InlineFile
    {
        public PropertyType Type;
    }

    [JsonName("CleanedESM")]
    public class CleanedESM : InlineFile
    {
        public Hash SourceESMHash;
    }

    /// <summary>
    ///     A file that has the game and MO2 folders remapped on installation
    /// </summary>
    [JsonName("RemappedInlineFile")]
    public class RemappedInlineFile : InlineFile
    {
    }

    [JsonName("SteamMeta")]
    public class SteamMeta : ArchiveMeta
    {
        public int ItemID { get; set; }
    }

    [JsonName("FromArchive")]
    public class FromArchive : Directive
    {
        private string? _fullPath;

        public HashRelativePath ArchiveHashPath { get; set; }

        [JsonIgnore]
        public VirtualFile? FromFile { get; set; }

        [JsonIgnore]
        public string FullPath => _fullPath ??= string.Join("|", ArchiveHashPath);
    }

    [JsonName("CreateBSA")]
    public class CreateBSA : Directive
    {
        public RelativePath TempID { get; set; }
        public ArchiveStateObject State { get; }
        public List<FileStateObject> FileStates { get; set; } = new List<FileStateObject>();

        public CreateBSA(ArchiveStateObject state, IEnumerable<FileStateObject>? items = null)
        {
            State = state;
            if (items != null)
            {
                FileStates.AddRange(items);
            }
        }
    }

    [JsonName("PatchedFromArchive")]
    public class PatchedFromArchive : FromArchive
    {
        public Hash FromHash { get; set; }

        /// <summary>
        ///     The file to apply to the source file to patch it
        /// </summary>
        public RelativePath PatchID { get; set; }
    }

    [JsonName("SourcePatch")]
    public class SourcePatch
    {
        public Hash Hash { get; set; }
        public RelativePath RelativePath { get; set; }
    }

    [JsonName("MergedPatch")]
    public class MergedPatch : Directive
    {
        public RelativePath PatchID { get; set; }
        public List<SourcePatch> Sources { get; set; } = new List<SourcePatch>();
    }

    [JsonName("Archive")]
    public class Archive
    {
        /// <summary>
        ///     xxHash64 of the archive
        /// </summary>
        public Hash Hash { get; set; }

        /// <summary>
        ///     Meta INI for the downloaded archive
        /// </summary>
        public string? Meta { get; set; }

        /// <summary>
        ///     Human friendly name of this archive
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public long Size { get; set; }
        
        public AbstractDownloadState State { get; }

        public Archive(AbstractDownloadState state)
        {
            State = state;
        }
    }

    public class IndexedArchive
    {
        public dynamic? IniData;
        public string Meta = string.Empty;
        public string Name = string.Empty;
        public VirtualFile File { get; }

        public IndexedArchive(VirtualFile file)
        {
            File = file;
        }
    }
}
