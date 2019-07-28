using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static BSA.Tools.libbsarch;

namespace BSA.Tools
{
    // Represents a BSA archive on disk (in READ mode)
    public class BSAFile : IDisposable
    {
        private static Mutex GlobalLock = new Mutex(false);
        protected unsafe libbsarch.bsa_archive_t* _archive;

        public UInt32 Version
        {
            get
            {
                unsafe
                {
                    return libbsarch.bsa_version_get(_archive);
                }
            }
        }

        public bsa_archive_type_t Type
        {
            get
            {
                unsafe
                {
                    return libbsarch.bsa_archive_type_get(_archive);
                }
            }
        }

        public UInt32 FileCount
        {
            get
            {
                unsafe
                {
                    return libbsarch.bsa_file_count_get(_archive);
                }
            }
        }

        public UInt32 ArchiveFlags
        {
            get
            {
                unsafe
                {
                    return libbsarch.bsa_archive_flags_get(_archive);
                }
            }
            set
            {
                unsafe
                {
                    libbsarch.bsa_archive_flags_set(_archive, value);
                }
            }
        }

        public UInt32 FileFlags
        {
            get
            {
                unsafe
                {
                    return libbsarch.bsa_file_flags_get(_archive);
                }
            }
            set
            {
                unsafe
                {
                    libbsarch.bsa_file_flags_set(_archive, value);
                }
            }
        }

        public bool Compress
        {
            get
            {
                unsafe
                {
                    return libbsarch.bsa_compress_get(_archive);
                }
            }
            set
            {
                unsafe
                {
                    libbsarch.bsa_compress_set(_archive, value);
                }
            }
        }

        public bool ShareData
        {
            get
            {
                unsafe
                {
                    return libbsarch.bsa_share_data_get(_archive);
                }
            }
            set
            {
                unsafe
                {
                    libbsarch.bsa_share_data_set(_archive, value);
                }
            }
        }


        public void Save()
        {
            unsafe
            {
                check_err(libbsarch.bsa_save(_archive));
            }
        }

        private IEnumerable<ArchiveEntry> _entries = null;
        public IEnumerable<ArchiveEntry> Entries {
            get
            {
                if (_entries != null)
                    return _entries;

                return GetAndCacheEntries();
            }

                
        }

        private IEnumerable<ArchiveEntry> GetAndCacheEntries()
        {
            var entries = new List<ArchiveEntry>();
            unsafe
            {
                foreach (var filename in GetFileNames())
                {
                    entries.Add(new ArchiveEntry(this, _archive, filename));
                }
            }
            _entries = entries;
            return entries;
        }

        public BSAFile()
        {
            GlobalLock.WaitOne();
            unsafe
            {
                _archive = libbsarch.bsa_create();
            }
        }

        public void Create(string filename, bsa_archive_type_t type, EntryList entries)
        {
            unsafe
            {
                check_err(libbsarch.bsa_create_archive(_archive, filename, type, entries._list));
            }
        }

        public BSAFile(string filename)
        {
            GlobalLock.WaitOne();
            unsafe
            {
                _archive = libbsarch.bsa_create();
                check_err(libbsarch.bsa_load_from_file(_archive, filename));
            }
        }

        public void AddFile(string filename, byte[] data)
        {
            unsafe
            {
                var ptr = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, ptr, data.Length);
                libbsarch.bsa_add_file_from_memory(_archive, filename, (UInt32)data.Length, (byte*)ptr);
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void Dispose()
        {
            unsafe
            {
                check_err(libbsarch.bsa_free(_archive));
            }
            GlobalLock.ReleaseMutex();
        }

        public static void check_err(libbsarch.bsa_result_message_t bsa_result_message_t)
        {
            if (bsa_result_message_t.code != 0)
            {
                unsafe
                {
                    int i = 0;
                    for (i = 0; i < 1024 * 2; i += 2)
                        if (bsa_result_message_t.text[i] == 0) break;

                    var msg = new String((sbyte*)bsa_result_message_t.text, 0, i, Encoding.Unicode);
                    throw new Exception(msg);
                }
            }
        }

        public IEnumerable<string> GetFileNames()
        {
            List<string> filenames = new List<string>();
            unsafe
            {
                check_err(libbsarch.bsa_iterate_files(_archive, (archive, filename, file, folder, context) =>
                {
                    lock (filenames)
                    {
                        filenames.Add(filename);
                    }
                    return false;
                }, null));
            }
            return filenames;
        }
    }

    public  class ArchiveEntry
    {
        private BSAFile _archive;
        private unsafe libbsarch.bsa_archive_t* _archivep;
        private string _filename;

        public string Filename {
            get
            {
                return _filename;
            }
        }

        public unsafe ArchiveEntry(BSAFile archive, libbsarch.bsa_archive_t* archivep, string filename)
        {
            _archive = archive;
            _archivep = archivep;
            _filename = filename;
        }

        public FileData GetFileData()
        {
            unsafe
            {
                var result = libbsarch.bsa_extract_file_data_by_filename(_archivep, _filename);
                BSAFile.check_err(result.message);
                return new FileData(_archive, _archivep, result.buffer);
            }
        }

        public void ExtractTo(Stream stream)
        {
            using (var data = GetFileData())
            {
                data.WriteTo(stream);
            }
        }

        public void ExtractTo(string filename)
        {
            unsafe
            {
                libbsarch.bsa_extract_file(_archivep, _filename, filename);
            }
        }
    }

    public class FileData : IDisposable
    {
        private BSAFile archive;
        private unsafe libbsarch.bsa_archive_t* archivep;
        private libbsarch.bsa_result_buffer_t result;

        public unsafe FileData(BSAFile archive, libbsarch.bsa_archive_t* archivep, libbsarch.bsa_result_buffer_t result)
        {
            this.archive = archive;
            this.archivep = archivep;
            this.result = result;
        }

        public void WriteTo(Stream stream)
        {
            var memory = ToByteArray();
            stream.Write(memory, 0, (int)result.size);
        }

        public byte[] ToByteArray()
        {
            unsafe
            {
                byte[] memory = new byte[result.size];
                Marshal.Copy((IntPtr)result.data, memory, 0, (int)result.size);
                return memory;
            }
        }

        public void Dispose()
        {
            unsafe
            {
                BSAFile.check_err(libbsarch.bsa_file_data_free(archivep, result));
            }
        }
    }

    public class EntryList : IDisposable
    {
        public unsafe bsa_entry_list_t* _list;

        public EntryList()
        {
            unsafe
            {
                _list = libbsarch.bsa_entry_list_create();
            }
        }

        public UInt32 Count
        {
            get
            {
                lock (this)
                {
                    unsafe
                    {
                        return libbsarch.bsa_entry_list_count(_list);
                    }
                }
            }
        }

        public void Add(string entry)
        {
            lock(this)
            {
                unsafe
                {
                    libbsarch.bsa_entry_list_add(_list, entry);
                }
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                unsafe
                {
                    libbsarch.bsa_entry_list_free(_list);
                }
            }
        }
    }
}
