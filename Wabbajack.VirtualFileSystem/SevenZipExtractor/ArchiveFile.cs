using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;

namespace Wabbajack.VirtualFileSystem.SevenZipExtractor
{
    public class ArchiveFile : IDisposable
    {
        private SevenZipHandle _sevenZipHandle;
        internal IInArchive _archive;
        private InStreamWrapper _archiveStream;
        private IList<Entry> _entries;

        private static readonly AbsolutePath LibraryFilePath = @"Extractors\7z.dll".RelativeTo(AbsolutePath.EntryPoint);
        private static SignatureChecker _checker = new SignatureChecker(Formats.FileTypeGuidMapping.Keys.ToArray());

        public static async Task<ArchiveFile> Open(AbsolutePath archiveFilePath)
        {
            var self = new ArchiveFile();
            
            self.InitializeAndValidateLibrary();

            if (!archiveFilePath.IsFile)
            {
                throw new SevenZipException("Archive file not found");
            }

            var format = await _checker.MatchesAsync(archiveFilePath);

            if (format == null)
            {
                throw new SevenZipException($"Unknown format for {archiveFilePath}");
            }

            self._archive = self._sevenZipHandle.CreateInArchive(Formats.FileTypeGuidMapping[format.Value]);
            self._archiveStream = new InStreamWrapper(await archiveFilePath.OpenRead());
            return self;
        }
        
        public static async Task<ArchiveFile> Open(Stream archiveStream, Definitions.FileType format)
        {
            var self = new ArchiveFile();
            self.InitializeAndValidateLibrary();
            self._archive = self._sevenZipHandle.CreateInArchive(Formats.FileTypeGuidMapping[format]);
            self._archiveStream = new InStreamWrapper(archiveStream);
            return self;
        }

        public async Task Extract(AbsolutePath outputFolder, bool overwrite = false) 
        {
            await this.Extract(entry => 
            {
                var fileName = outputFolder.Combine(entry.FileName);

                if (!fileName.Exists || overwrite) 
                {
                    return fileName;
                }

                return default;
            });
        }

        public async Task Extract(Func<RelativePath, AbsolutePath> getOutputPath) 
        {
            IList<Stream> fileStreams = new List<Stream>();

            try 
            {
                foreach (Entry entry in Entries)
                {
                    
                    AbsolutePath outputPath = entry.IsFolder ? default : getOutputPath((RelativePath)entry.FileName);

                    if (outputPath == default) // getOutputPath = null means SKIP
                    {
                        fileStreams.Add(null);
                        continue;
                    }

                    if (entry.IsFolder)
                    {
                        outputPath.CreateDirectory();
                        fileStreams.Add(null);
                        continue;
                    }

                    var directoryName = outputPath.Parent;
                    directoryName.CreateDirectory();

                    fileStreams.Add(await outputPath.Create());
                }

                this._archive.Extract(null, 0xFFFFFFFF, 0, new ArchiveStreamsCallback(fileStreams));
            }
            finally
            {
                foreach (Stream stream in fileStreams)
                {
                    if (stream == null) continue;
                    var tsk = stream?.DisposeAsync();
                    await tsk.Value;
                }
            }
        }

        public IList<Entry> Entries
        {
            get
            {
                if (this._entries != null)
                {
                    return this._entries;
                }

                ulong checkPos = 32 * 1024;
                int open = this._archive.Open(this._archiveStream, ref checkPos, null);

                if (open != 0)
                {
                    throw new SevenZipException("Unable to open archive");
                }

                uint itemsCount = this._archive.GetNumberOfItems();

                this._entries = new List<Entry>();

                for (uint fileIndex = 0; fileIndex < itemsCount; fileIndex++)
                {
                    string fileName = this.GetProperty<string>(fileIndex, ItemPropId.kpidPath);
                    bool isFolder = this.GetProperty<bool>(fileIndex, ItemPropId.kpidIsFolder);
                    bool isEncrypted = this.GetProperty<bool>(fileIndex, ItemPropId.kpidEncrypted);
                    ulong size = this.GetProperty<ulong>(fileIndex, ItemPropId.kpidSize);
                    ulong packedSize = this.GetProperty<ulong>(fileIndex, ItemPropId.kpidPackedSize);
                    DateTime creationTime = this.GetPropertySafe<DateTime>(fileIndex, ItemPropId.kpidCreationTime);
                    DateTime lastWriteTime = this.GetPropertySafe<DateTime>(fileIndex, ItemPropId.kpidLastWriteTime);
                    DateTime lastAccessTime = this.GetPropertySafe<DateTime>(fileIndex, ItemPropId.kpidLastAccessTime);
                    uint crc = this.GetPropertySafe<uint>(fileIndex, ItemPropId.kpidCRC);
                    uint attributes = this.GetPropertySafe<uint>(fileIndex, ItemPropId.kpidAttributes);
                    string comment = this.GetPropertySafe<string>(fileIndex, ItemPropId.kpidComment);
                    string hostOS = this.GetPropertySafe<string>(fileIndex, ItemPropId.kpidHostOS);
                    string method = this.GetPropertySafe<string>(fileIndex, ItemPropId.kpidMethod);

                    bool isSplitBefore = this.GetPropertySafe<bool>(fileIndex, ItemPropId.kpidSplitBefore);
                    bool isSplitAfter = this.GetPropertySafe<bool>(fileIndex, ItemPropId.kpidSplitAfter);

                    this._entries.Add(new Entry(this._archive, fileIndex)
                    {
                        FileName = fileName,
                        IsFolder = isFolder,
                        IsEncrypted = isEncrypted,
                        Size = size,
                        PackedSize = packedSize,
                        CreationTime = creationTime,
                        LastWriteTime = lastWriteTime,
                        LastAccessTime = lastAccessTime,
                        CRC = crc,
                        Attributes = attributes,
                        Comment = comment,
                        HostOS = hostOS,
                        Method = method,
                        IsSplitBefore = isSplitBefore,
                        IsSplitAfter = isSplitAfter
                    });
                }

                return this._entries;
            }
        }

        private T GetPropertySafe<T>(uint fileIndex, ItemPropId name)
        {
            try
            {
                return this.GetProperty<T>(fileIndex, name);
            }
            catch (InvalidCastException)
            {
                return default(T);
            }
        }

        private T GetProperty<T>(uint fileIndex, ItemPropId name)
        {
            PropVariant propVariant = new PropVariant();
            this._archive.GetProperty(fileIndex, name, ref propVariant);
            object value = propVariant.GetObject();

            if (propVariant.VarType == VarEnum.VT_EMPTY)
            {
                propVariant.Clear();
                return default(T);
            }

            propVariant.Clear();

            if (value == null)
            {
                return default(T);
            }

            Type type = typeof(T);
            bool isNullable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
            Type underlyingType = isNullable ? Nullable.GetUnderlyingType(type) : type;

            T result = (T)Convert.ChangeType(value.ToString(), underlyingType);

            return result;
        }

        private void InitializeAndValidateLibrary()
        {
            try
            {
                this._sevenZipHandle = new SevenZipHandle((string)LibraryFilePath);
            }
            catch (Exception e)
            {
                throw new SevenZipException("Unable to initialize SevenZipHandle", e);
            }
        }

        ~ArchiveFile()
        {
            this.Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            if (this._archiveStream != null)
            {
                this._archiveStream.Dispose();
            }

            if (this._archive != null)
            {
                Marshal.ReleaseComObject(this._archive);
            }

            if (this._sevenZipHandle != null)
            {
                this._sevenZipHandle.Dispose();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
