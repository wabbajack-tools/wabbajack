using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using VFS;
using Wabbajack.Common;

namespace Wabbajack
{
    public class RawSourceFile
    {
        public string Path;

        public RawSourceFile(VirtualFile file)
        {
            File = file;
        }

        public string AbsolutePath => File.StagedPath;

        public VirtualFile File { get; }

        public string Hash => File.Hash;

        public T EvolveTo<T>() where T : Directive, new()
        {
            var v = new T();
            v.To = Path;
            v.Hash = Hash;
            v.Size = File.Size;
            return v;
        }
    }

    [Serializable]
    public class ModList
    {
        /// <summary>
        ///     Archives required by this modlist
        /// </summary>
        public List<Archive> Archives;

        /// <summary>
        /// The game variant to which this game applies
        /// </summary>
        public Game GameType;

        /// <summary>
        ///     Install directives
        /// </summary>
        public List<Directive> Directives;

        /// <summary>
        ///     Name of the ModList
        /// </summary>
        public string Name;

        /// <summary>
        ///     Content Report in HTML form
        /// </summary>
        public string ReportHTML;
    }

    [Serializable]
    public class Directive
    {
        /// <summary>
        ///     location the file will be copied to, relative to the install path.
        /// </summary>
        public string To;
        public long Size;
        public string Hash;
    }

    [Serializable]
    public class IgnoredDirectly : Directive
    {
        public string Reason;
    }

    [Serializable]
    public class NoMatch : IgnoredDirectly
    {
    }

    [Serializable]
    public class InlineFile : Directive
    {
        /// <summary>
        ///     Data that will be written as-is to the destination location;
        /// </summary>
        public string SourceData;
    }

    [Serializable]
    public class CleanedESM : InlineFile
    {
        public string SourceESMHash;
    }

    /// <summary>
    ///     A file that has the game and MO2 folders remapped on installation
    /// </summary>
    [Serializable]
    public class RemappedInlineFile : InlineFile
    {
    }

    [Serializable]
    public class FromArchive : Directive
    {
        private string _fullPath;

        /// <summary>
        ///     MurMur3 hash of the archive this file comes from
        /// </summary>
        public string[] ArchiveHashPath;

        [JsonIgnore] [NonSerialized] public VirtualFile FromFile;

        [JsonIgnore]
        public string FullPath
        {
            get
            {
                if (_fullPath == null) _fullPath = string.Join("|", ArchiveHashPath);
                return _fullPath;
            }
        }
    }

    [Serializable]
    public class CreateBSA : Directive
    {
        public string IsCompressed;
        public bool ShareData;
        public string TempID;
        public uint Type;
        public uint Version;

        public uint FileFlags { get; set; }
        public bool Compress { get; set; }
        public uint ArchiveFlags { get; set; }
    }

    [Serializable]
    public class PatchedFromArchive : FromArchive
    {
        public string Hash;

        /// <summary>
        ///     The file to apply to the source file to patch it
        /// </summary>
        public byte[] Patch;
    }

    [Serializable]
    public class SourcePatch
    {
        public string RelativePath;
        public string Hash;
    }

    [Serializable]
    public class MergedPatch : Directive
    {
        public List<SourcePatch> Sources;
        public string Hash;
        public byte[] Patch;
    }

    [Serializable]
    public class Archive
    {
        /// <summary>
        ///     MurMur3 Hash of the archive
        /// </summary>
        public string Hash;

        /// Meta INI for the downloaded archive
        /// </summary>
        public string Meta;

        /// <summary>
        ///     Human friendly name of this archive
        /// </summary>
        public string Name;

        public long Size;
    }

    [Serializable]
    public class NexusMod : Archive
    {
        public string Author;
        public string FileID;
        public string GameName;
        public string ModID;
        public string UploadedBy;
        public string UploaderProfile;
        public string Version;
        public string SlideShowPic;
        public string ModName;
        public string NexusURL;
        public string Summary;
    }

    [Serializable]
    public class ManualArchive : Archive
    {
        public string URL;
        public string Notes;
    }

    [Serializable]
    public class GoogleDriveMod : Archive
    {
        public string Id;
    }

    /// <summary>
    ///     URL that can be downloaded directly without any additional options
    /// </summary>
    [Serializable]
    public class DirectURLArchive : Archive
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Headers;

        public string URL;
    }

    /// <summary>
    ///     An archive that requires additional HTTP headers.
    /// </summary>
    [Serializable]
    public class DirectURLArchiveEx : DirectURLArchive
    {
        public Dictionary<string, string> Headers;
    }

    /// <summary>
    ///     Archive that comes from MEGA
    /// </summary>
    [Serializable]
    public class MEGAArchive : DirectURLArchive
    {
    }

    /// <summary>
    ///     Archive that comes from MODDB
    /// </summary>
    [Serializable]
    public class MODDBArchive : DirectURLArchive
    {
    }

    /// <summary>
    ///     Archive that comes from MediaFire
    /// </summary>
    [Serializable]
    public class MediaFireArchive : DirectURLArchive
    {
    }

    [Serializable]
    public class IndexedArchive
    {
        public dynamic IniData;
        public string Meta;
        public string Name;
        public VirtualFile File { get; internal set; }
    }

    /// <summary>
    ///     A archive entry
    /// </summary>
    [Serializable]
    public class IndexedEntry
    {
        /// <summary>
        ///     MurMur3 hash of this file
        /// </summary>
        public string Hash;

        /// <summary>
        ///     Path in the archive to this file
        /// </summary>
        public string Path;

        /// <summary>
        ///     Size of the file (uncompressed)
        /// </summary>
        public long Size;
    }

    [Serializable]
    public class IndexedArchiveEntry : IndexedEntry
    {
        public string[] HashPath;
    }

    /// <summary>
    ///     Data found inside a BSA file in an archive
    /// </summary>
    [Serializable]
    public class BSAIndexedEntry : IndexedEntry
    {
        /// <summary>
        ///     MurMur3 hash of the BSA this file comes from
        /// </summary>
        public string BSAHash;
    }
}