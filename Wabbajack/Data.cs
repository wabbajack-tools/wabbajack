using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using VFS;

namespace Wabbajack
{
    public class RawSourceFile 
    {
        public RawSourceFile(VirtualFile file)
        {
            File = file;
        }

        public string AbsolutePath
        {
            get
            {
                return File.StagedPath;
            }
        }
        
        public string Path;

        public VirtualFile File { get; private set; }

        public string Hash
        {
            get
            {
                return File.Hash;
            }

        }

        public T EvolveTo<T>() where T : Directive, new()
        {
            var v = new T();
            v.To = Path;
            return v;
        }
    }

    [Serializable]
    public class ModList
    {
        /// <summary>
        /// Name of the ModList
        /// </summary>
        public string Name;

        /// <summary>
        /// Install directives
        /// </summary>
        public List<Directive> Directives;

        /// <summary>
        /// Archives required by this modlist
        /// </summary>
        public List<Archive> Archives;

        /// <summary>
        /// Content Report in HTML form
        /// </summary>
        public string ReportHTML;
    }

    [Serializable]
    public class Directive
    {
        /// <summary>
        /// location the file will be copied to, relative to the install path.
        /// </summary>
        public string To;
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
        /// Data that will be written as-is to the destination location;
        /// </summary>
        public string SourceData;
    }

    [Serializable]
    public class CleanedESM : InlineFile
    {
        public string SourceESMHash;
    }

    /// <summary>
    /// A file that has the game and MO2 folders remapped on installation
    /// </summary>
    [Serializable]
    public class RemappedInlineFile : InlineFile
    {
    }

    [Serializable]
    public class FromArchive : Directive
    {
        /// <summary>
        /// MurMur3 hash of the archive this file comes from
        /// </summary>
        public string[] ArchiveHashPath;

        [JsonIgnore]
        [NonSerialized]
        public VirtualFile FromFile;

        private string _fullPath = null;

        [JsonIgnore]
        public string FullPath
        {
            get
            {
                if (_fullPath == null) {
                    _fullPath = String.Join("|", ArchiveHashPath);
                }
                return _fullPath;
            }
        }
    }

    [Serializable]
    public class CreateBSA : Directive
    {
        public string TempID;
        public string IsCompressed;
        public uint Version;
        public uint Type;
        public bool ShareData;

        public uint FileFlags { get; set; }
        public bool Compress { get; set; }
        public uint ArchiveFlags { get; set; }
    }

    [Serializable]
    public class PatchedFromArchive : FromArchive
    {
        /// <summary>
        /// The file to apply to the source file to patch it
        /// </summary>
        public byte[] Patch;
    }

    [Serializable]
    public class Archive
    {
        /// <summary>
        /// MurMur3 Hash of the archive
        /// </summary>
        public string Hash;
        /// <summary>
        /// Human friendly name of this archive
        /// </summary>
        public string Name;

        /// Meta INI for the downloaded archive
        /// </summary>
        public string Meta;

        public long Size;
    }

    [Serializable]
    public class NexusMod : Archive
    {
        public string GameName;
        public string ModID;
        public string FileID;
        public string Version;
        public string UploaderProfile;
        public string UploadedBy;
        public string Author;
    }

    [Serializable]
    public class GoogleDriveMod : Archive
    {
        public string Id;
    }

    /// <summary>
    /// URL that can be downloaded directly without any additional options
    /// </summary>
    [Serializable]
    public class DirectURLArchive : Archive
    {
        public string URL;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Headers;
    }

    /// <summary>
    /// A URL that cannot be downloaded automatically and has to be downloaded by hand
    /// </summary>
    [Serializable]
    public class ManualURLArchive : Archive
    {
        public string URL;
    }

    /// <summary>
    /// An archive that requires additional HTTP headers.
    /// </summary>
    [Serializable]
    public class DirectURLArchiveEx : DirectURLArchive
    {
        public Dictionary<string, string> Headers;
    }

    /// <summary>
    /// Archive that comes from MEGA
    /// </summary>
    [Serializable]
    public class MEGAArchive : DirectURLArchive
    {
    }

    /// <summary>
    /// Archive that comes from MODDB
    /// </summary>
    [Serializable]
    public class MODDBArchive : DirectURLArchive
    {
    }

    /// <summary>
    /// Archive that comes from MediaFire
    /// </summary>
    [Serializable]
    public class MediaFireArchive : DirectURLArchive
    {
    }

    [Serializable]
    public class IndexedArchive
    {
        public dynamic IniData;
        public string Name;
        public string Meta;
        public VirtualFile File { get; internal set; }
    }

    /// <summary>
    /// A archive entry
    /// </summary>
    [Serializable]
    public class IndexedEntry
    {
        /// <summary>
        /// Path in the archive to this file
        /// </summary>
        public string Path;
        /// <summary>
        /// MurMur3 hash of this file
        /// </summary>
        public string Hash;
        /// <summary>
        /// Size of the file (uncompressed)
        /// </summary>
        public long Size;
    }

    [Serializable]
    public class IndexedArchiveEntry : IndexedEntry
    {
        public string[] HashPath;
    }

    /// <summary>
    /// Data found inside a BSA file in an archive
    /// </summary>
    [Serializable]
    public class BSAIndexedEntry : IndexedEntry
    {
        /// <summary>
        /// MurMur3 hash of the BSA this file comes from
        /// </summary>
        public string BSAHash;
    }
}
