using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Compression.BSA;
using MessagePack;
using Wabbajack.Common;
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

        public AbsolutePath AbsolutePath => File.StagedPath;

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

    [MessagePackObject]
    public class ModList
    {
        /// <summary>
        ///     Archives required by this modlist
        /// </summary>
        [Key(0)]
        public List<Archive> Archives;

        /// <summary>
        ///     Author of the ModList
        /// </summary>
        [Key(1)]
        public string Author;

        /// <summary>
        ///     Description of the ModList
        /// </summary>
        [Key(2)]
        public string Description;

        /// <summary>
        ///     Install directives
        /// </summary>
        [Key(3)]
        public List<Directive> Directives;

        /// <summary>
        ///     The game variant to which this game applies
        /// </summary>
        [Key(4)]
        public Game GameType;

        /// <summary>
        ///     Hash of the banner-image
        /// </summary>
        [Key(5)]
        public string Image;

        /// <summary>
        ///     The Mod Manager used to create the modlist
        /// </summary>
        [Key(6)]
        public ModManager ModManager;

        /// <summary>
        ///     Name of the ModList
        /// </summary>
        [Key(7)]
        public string Name;

        /// <summary>
        ///     readme path or website
        /// </summary>
        [Key(8)]
        public string Readme;

        /// <summary>
        ///     Whether readme is a website
        /// </summary>
        [Key(9)]
        public bool ReadmeIsWebsite;

        /// <summary>
        ///     The build version of Wabbajack used when compiling the Modlist
        /// </summary>
        [Key(10)]
        public Version WabbajackVersion;

        /// <summary>
        ///     Website of the ModList
        /// </summary>
        [Key(11)]
        public Uri Website;

        /// <summary>
        ///     The size of all the archives once they're downloaded
        /// </summary>
        [IgnoreMember]
        public long DownloadSize => Archives.Sum(a => a.Size);

        /// <summary>
        ///     The size of all the files once they are installed (excluding downloaded archives)
        /// </summary>
        [IgnoreMember]
        public long InstallSize => Directives.Sum(s => s.Size);

        public ModList Clone()
        {
            using var ms = new MemoryStream();
            ms.WriteAsMessagePack(this);
            ms.Position = 0;
            return ms.ReadAsMessagePack<ModList>();
        }
    }

    [MessagePackObject]
    [Union(0, typeof(ArchiveMeta))]
    [Union(1, typeof(CreateBSA))]
    [Union(2, typeof(FromArchive))]
    [Union(3, typeof(MergedPatch))]
    [Union(4, typeof(InlineFile))]
    [Union(5, typeof(PatchedFromArchive))]
    [Union(6, typeof(RemappedInlineFile))]
    public abstract class Directive
    {
        [Key(0)]
        public Hash Hash { get; set; }
        [Key(1)]
        public long Size { get; set; }

        /// <summary>
        ///     location the file will be copied to, relative to the install path.
        /// </summary>
        [Key(2)]
        public RelativePath To { get; set; }
    }

    public class IgnoredDirectly : Directive
    {
        public string Reason;
    }

    public class NoMatch : IgnoredDirectly
    {
    }

    [MessagePackObject]
    public class InlineFile : Directive
    {
        /// <summary>
        ///     Data that will be written as-is to the destination location;
        /// </summary>
        [Key(3)]
        public RelativePath SourceDataID { get; set; }
    }

    [MessagePackObject]
    public class ArchiveMeta : Directive
    {
        [Key(3)]
        public RelativePath SourceDataID { get; set; }
    }

    public enum PropertyType { Banner, Readme }

    /// <summary>
    ///     File meant to be extracted before the installation
    /// </summary>
    public class PropertyFile : InlineFile
    {
        public PropertyType Type;
    }

    public class CleanedESM : InlineFile
    {
        public Hash SourceESMHash;
    }

    /// <summary>
    ///     A file that has the game and MO2 folders remapped on installation
    /// </summary>
    [MessagePackObject]
    public class RemappedInlineFile : InlineFile
    {
    }

    [MessagePackObject]
    public class SteamMeta : ArchiveMeta
    {
        [Key(4)]
        public int ItemID { get; set; }
    }

    [MessagePackObject]
    public class FromArchive : Directive
    {
        private string _fullPath;

        [Key(3)]
        public HashRelativePath ArchiveHashPath { get; set; }

        [IgnoreMember]
        public VirtualFile FromFile { get; set; }

        [IgnoreMember]
        public string FullPath => _fullPath ??= string.Join("|", ArchiveHashPath);
    }

    [MessagePackObject]
    public class CreateBSA : Directive
    {
        [Key(3)]
        public string TempID { get; set; }
        [Key(4)]
        public ArchiveStateObject State { get; set; }
        [Key(5)]
        public List<FileStateObject> FileStates { get; set; }
    }

    [MessagePackObject]
    public class PatchedFromArchive : FromArchive
    {
        [Key(4)]
        public Hash FromHash { get; set; }

        /// <summary>
        ///     The file to apply to the source file to patch it
        /// </summary>
        [Key(5)]
        public string PatchID { get; set; }
    }

    [MessagePackObject]
    public class SourcePatch
    {
        [Key(0)]
        public Hash Hash { get; set; }
        [Key(1)]
        public RelativePath RelativePath { get; set; }
    }

    [MessagePackObject]
    public class MergedPatch : Directive
    {
        [Key(3)]
        public string PatchID { get; set; }
        [Key(4)]
        public List<SourcePatch> Sources { get; set; }
    }

    [MessagePackObject]
    public class Archive
    {
        /// <summary>
        ///     MurMur3 Hash of the archive
        /// </summary>
        [Key(0)]
        public Hash Hash { get; set; }

        /// <summary>
        ///     Meta INI for the downloaded archive
        /// </summary>
        [Key(1)]
        public string Meta { get; set; }

        /// <summary>
        ///     Human friendly name of this archive
        /// </summary>
        [Key(2)]
        public string Name { get; set; }

        [Key(3)]
        public long Size { get; set; }
        
        [Key(4)]
        public AbstractDownloadState State { get; set; }
    }

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

    public class IndexedArchiveEntry : IndexedEntry
    {
        public string[] HashPath;
    }
}
