using System;
using System.Collections.Generic;
using Ceras;
using Compression.BSA;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Lib
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
        /// The build version of Wabbajack used when compiling the Modlist
        /// </summary>
        public string WabbajackVersion;

        /// <summary>
        ///     Install directives
        /// </summary>
        public List<Directive> Directives;

        /// <summary>
        ///     Name of the ModList
        /// </summary>
        public string Name;

        /// <summary>
        ///     Author of the ModList
        /// </summary>
        public string Author;

        /// <summary>
        ///     Description of the ModList
        /// </summary>
        public string Description;

        /// <summary>
        ///     Hash of the banner-image
        /// </summary>
        public string Image;

        /// <summary>
        ///     Website of the ModList
        /// </summary>
        public string Website;

        /// <summary>
        ///     Hash of the readme
        /// </summary>
        public string Readme;

        /// <summary>
        ///     Content Report in HTML form
        /// </summary>
        public string ReportHTML;
    }

    public class Directive
    {
        /// <summary>
        ///     location the file will be copied to, relative to the install path.
        /// </summary>
        public string To;
        public long Size;
        public string Hash;
    }

    public class IgnoredDirectly : Directive
    {
        public string Reason;
    }

    public class NoMatch : IgnoredDirectly
    {
    }

    public class InlineFile : Directive
    {
        /// <summary>
        ///     Data that will be written as-is to the destination location;
        /// </summary>
        public string SourceDataID;
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
        public string SourceESMHash;
    }

    /// <summary>
    ///     A file that has the game and MO2 folders remapped on installation
    /// </summary>
    public class RemappedInlineFile : InlineFile
    {
    }

    [MemberConfig(TargetMember.All)]
    public class FromArchive : Directive
    {
        private string _fullPath;

        /// <summary>
        ///     MurMur3 hash of the archive this file comes from
        /// </summary>
        public string[] ArchiveHashPath;

        [Exclude]
        public VirtualFile FromFile;

        [Exclude]
        public string FullPath
        {
            get
            {
                if (_fullPath == null) _fullPath = string.Join("|", ArchiveHashPath);
                return _fullPath;
            }
        }
    }

    public class CreateBSA : Directive
    {
        public string TempID;
        public uint Type;
        public ArchiveStateObject State { get; set; }
        public List<FileStateObject> FileStates { get; set; }
    }

    public class PatchedFromArchive : FromArchive
    {
        /// <summary>
        ///     The file to apply to the source file to patch it
        /// </summary>
        public string PatchID;
    }

    public class SourcePatch
    {
        public string RelativePath;
        public string Hash;
    }

    public class MergedPatch : Directive
    {
        public List<SourcePatch> Sources;
        public string PatchID;
    }

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

    /// <summary>
    ///     Data found inside a BSA file in an archive
    /// </summary>
    public class BSAIndexedEntry : IndexedEntry
    {
        /// <summary>
        ///     MurMur3 hash of the BSA this file comes from
        /// </summary>
        public string BSAHash;
    }
}