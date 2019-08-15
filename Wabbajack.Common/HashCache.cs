using Compression.BSA;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common
{

    public class HashCache : IDisposable
    {
        public class Entry
        {
            public string name;
            public string hash;
            public long size;
            public DateTime last_modified;
        }

        public class BSA
        {
            public string full_path;
            public string hash;
            public long size;
            public DateTime last_modified;
            public Dictionary<string, string> entries;
        }

        private ConcurrentDictionary<string, Entry> _hashes = new ConcurrentDictionary<string, Entry>();
        private ConcurrentDictionary<string, BSA> _bsas = new ConcurrentDictionary<string, BSA>();
        private bool disposed;

        public class DB
        {
            public List<Entry> entries;
            public List<BSA> bsas;
        }

        public HashCache()
        {
            if (Consts.HashCacheName.FileExists())
            {
                var json = Consts.HashCacheName.FromJSON<DB>();
                _hashes = new ConcurrentDictionary<string, Entry>(json.entries.Select(e => new KeyValuePair<string, Entry>(e.name, e)));
                _bsas = new ConcurrentDictionary<string, BSA>(json.bsas.Select(e => new KeyValuePair<string, BSA>(e.full_path, e)));
            }
        }

        public string HashFile(string filename)
        {
            TOP:
            var result = _hashes.GetOrAdd(filename,
                s =>
                {
                    var fi = new FileInfo(filename);
                    return new Entry
                    {
                        name = filename,
                        hash = Utils.FileSHA256(filename),
                        size = fi.Length,
                        last_modified = fi.LastWriteTimeUtc
                    };
                });

            var info = new FileInfo(filename);
            if (info.LastWriteTimeUtc != result.last_modified || info.Length != result.size)
            {
                _hashes.TryRemove(filename, out Entry v);
                goto TOP;
            }
            return result.hash;
        }

        public void Dispose()
        {
            if (disposed) return;
            new DB
            {
                entries = _hashes.Values.ToList(),
                bsas = _bsas.Values.ToList()
            }.ToJSON(Consts.HashCacheName);
            disposed = true;
            _hashes = null;
            _bsas = null;
        }

        public List<(string, string)> HashBSA(string absolutePath, Action<string> status)
        {
            TOP:
            var finfo = new FileInfo(absolutePath);
            if (_bsas.TryGetValue(absolutePath, out BSA ar))
            {
                if (ar.last_modified == finfo.LastWriteTimeUtc && ar.size == finfo.Length)
                    return ar.entries.Select(kv => (kv.Key, kv.Value)).ToList();

                _bsas.TryRemove(absolutePath, out BSA value);
            }

            var bsa = new BSA()
            {
                full_path = absolutePath,
                size = finfo.Length,
                last_modified = finfo.LastAccessTimeUtc,
            };

            var entries = new ConcurrentBag<(string, string)>();
            status($"Hashing BSA: {absolutePath}");

            using (var a = new BSAReader(absolutePath))
            {
                a.Files.PMap(entry =>
                {
                    status($"Hashing BSA: {absolutePath} - {entry.Path}");
                    var data = entry.GetData();
                    entries.Add((entry.Path, data.SHA256()));
                });
            }
            bsa.entries = entries.ToDictionary(e => e.Item1, e => e.Item2);
            _bsas.TryAdd(absolutePath, bsa);

            goto TOP;
        }
    }
}
