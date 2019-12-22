using System;
using System.Collections.Generic;
using System.Data.HashFunction.xxHash;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Ceras;
using ICSharpCode.SharpZipLib.BZip2;
using IniParser;
using Newtonsoft.Json;
using ReactiveUI;
using Syroot.Windows.IO;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Common.StatusFeed.Errors;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Directory = System.IO.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{
    public static class Utils
    {
        public static bool IsMO2Running(string mo2Path)
        {
            Process[] processList = Process.GetProcesses();
            return processList.Where(process => process.ProcessName == "ModOrganizer").Any(process => Path.GetDirectoryName(process.MainModule?.FileName) == mo2Path);
        }

        public static string LogFile { get; private set; }

        public enum FileEventType
        {
            Created,
            Changed,
            Deleted
        }

        static Utils()
        {
            if (!Directory.Exists(Consts.LocalAppDataPath))
                Directory.CreateDirectory(Consts.LocalAppDataPath);

            var programName = Assembly.GetEntryAssembly()?.Location ?? "Wabbajack";
            LogFile = programName + ".log";
            _startTime = DateTime.Now;

            if (LogFile.FileExists())
                File.Delete(LogFile);

            var watcher = new FileSystemWatcher(Consts.LocalAppDataPath);
            AppLocalEvents = Observable.Merge(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => watcher.Changed += h, h => watcher.Changed -= h).Select(e => (FileEventType.Changed, e.EventArgs)),
                                                Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => watcher.Created += h, h => watcher.Created -= h).Select(e => (FileEventType.Created, e.EventArgs)),
                                                Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => watcher.Deleted += h, h => watcher.Deleted -= h).Select(e => (FileEventType.Deleted, e.EventArgs)))
                                       .ObserveOn(RxApp.TaskpoolScheduler);
            watcher.EnableRaisingEvents = true;
        }

        private static readonly Subject<IStatusMessage> LoggerSubj = new Subject<IStatusMessage>();
        public static IObservable<IStatusMessage> LogMessages => LoggerSubj;

        private static readonly string[] Suffix = {"B", "KB", "MB", "GB", "TB", "PB", "EB"}; // Longs run out around EB

        private static object _lock = new object();

        private static DateTime _startTime;

        
        public static void Log(string msg)
        {
            Log(new GenericInfo(msg));
        }

        public static T Log<T>(T msg) where T : IStatusMessage
        {
            LogToFile(msg.ExtendedDescription);
            LoggerSubj.OnNext(msg);
            return msg;
        }

        public static void Error(Exception ex, string extraMessage = null)
        {
            Log(new GenericException(ex, extraMessage));
        }

        public static void ErrorThrow(Exception ex, string extraMessage = null)
        {
            Error(ex, extraMessage);
            throw ex;
        }

        public static void Error(IException err)
        {
            LogToFile($"{err.ShortDescription}\n{err.Exception.StackTrace}");
            LoggerSubj.OnNext(err);
        }

        public static void ErrorThrow(IException err)
        {
            Error(err);
            throw err.Exception;
        }

        private static void LogToFile(string msg)
        {
            lock (_lock)
            {
                File.AppendAllText(LogFile, $"{(DateTime.Now - _startTime).TotalSeconds:0.##} - {msg}\r\n");
            }
        }

        public static void Status(string msg, int progress = 0, bool alsoLog = false)
        {
            WorkQueue.AsyncLocalCurrentQueue.Value?.Report(msg, progress);
            if (alsoLog)
            {
                Utils.Log(msg);
            }
        }

        /// <summary>
        ///     MurMur3 hashes the file pointed to by this string
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string FileSHA256(this string file)
        {
            var sha = new SHA256Managed();
            using (var o = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            {
                using (var i = File.OpenRead(file))
                {
                    i.CopyToWithStatus(new FileInfo(file).Length, o, $"Hashing {Path.GetFileName(file)}");
                }
            }

            return sha.Hash.ToBase64();
        }

        public static string FileHash(this string file, bool nullOnIOError = false)
        {
            try
            {
                var hash = new xxHashConfig();
                hash.HashSizeInBits = 64;
                hash.Seed = 0x42;
                using (var fs = File.OpenRead(file))
                {
                    var config = new xxHashConfig();
                    config.HashSizeInBits = 64;
                    using (var f = new StatusFileStream(fs, $"Hashing {Path.GetFileName(file)}"))    
                    {
                        var value = xxHashFactory.Instance.Create(config).ComputeHash(f);
                        return value.AsBase64String();
                    }
                }
            }
            catch (IOException ex)
            {
                if (nullOnIOError) return null;
                throw ex;
            }
        }

        public static string FileHashCached(this string file, bool nullOnIOError = false)
        {
            var hashPath = file + Consts.HashFileExtension;
            if (File.Exists(hashPath) && File.GetLastWriteTime(file) <= File.GetLastWriteTime(hashPath))
            {
                return File.ReadAllText(hashPath);
            }

            var hash = file.FileHash(nullOnIOError);
            File.WriteAllText(hashPath, hash);
            return hash;
        }

        public static async Task<string> FileHashAsync(this string file, bool nullOnIOError = false)
        {
            try
            {
                var hash = new xxHashConfig();
                hash.HashSizeInBits = 64;
                hash.Seed = 0x42;
                using (var fs = File.OpenRead(file))
                {
                    var config = new xxHashConfig();
                    config.HashSizeInBits = 64;
                    var value = await xxHashFactory.Instance.Create(config).ComputeHashAsync(fs);
                    return value.AsBase64String();
                }
            }
            catch (IOException ex)
            {
                if (nullOnIOError) return null;
                throw ex;
            }
        }

        public static void CopyToWithStatus(this Stream istream, long maxSize, Stream ostream, string status)
        {
            var buffer = new byte[1024 * 64];
            if (maxSize == 0) maxSize = 1;
            long totalRead = 0;
            while (true)
            {
                var read = istream.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                totalRead += read;
                ostream.Write(buffer, 0, read);
                Status(status, (int) (totalRead * 100 / maxSize));
            }
        }
        public static string xxHash(this byte[] data, bool nullOnIOError = false)
        {
            try
            {
                var hash = new xxHashConfig();
                hash.HashSizeInBits = 64;
                hash.Seed = 0x42;
                using (var fs = new MemoryStream(data))
                {
                    var config = new xxHashConfig();
                    config.HashSizeInBits = 64;
                    using (var f = new StatusFileStream(fs, $"Hashing memory stream"))
                    {
                        var value = xxHashFactory.Instance.Create(config).ComputeHash(f);
                        return value.AsBase64String();
                    }
                }
            }
            catch (IOException ex)
            {
                if (nullOnIOError) return null;
                throw ex;
            }
        }

        /// <summary>
        ///     Returns a Base64 encoding of these bytes
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToBase64(this byte[] data)
        {
            return Convert.ToBase64String(data);
        }

        public static string ToHex(this byte[] bytes)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        public static byte[] FromHex(this string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

        public static DateTime AsUnixTime(this long timestamp)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(timestamp);
            return dtDateTime;
        }

        /// <summary>
        ///     Returns data from a base64 stream
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] FromBase64(this string data)
        {
            return Convert.FromBase64String(data);
        }

        /// <summary>
        ///     Executes the action for every item in coll
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="coll"></param>
        /// <param name="f"></param>
        public static void Do<T>(this IEnumerable<T> coll, Action<T> f)
        {
            foreach (var i in coll) f(i);
        }

        public static void DoIndexed<T>(this IEnumerable<T> coll, Action<int, T> f)
        {
            var idx = 0;
            foreach (var i in coll)
            {
                f(idx, i);
                idx += 1;
            }
        }


        /// <summary>
        ///     Loads INI data from the given filename and returns a dynamic type that
        ///     can use . operators to navigate the INI.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static dynamic LoadIniFile(this string file)
        {
            return new DynamicIniData(new FileIniDataParser().ReadFile(file));
        }

        /// <summary>
        /// Loads a INI from the given string
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static dynamic LoadIniString(this string file)
        {
            return new DynamicIniData(new FileIniDataParser().ReadData(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(file)))));
        }

        public static void ToCERAS<T>(this T obj, string filename, SerializerConfig config)
        {
            var ceras = new CerasSerializer(config);
            byte[] buffer = null;
            ceras.Serialize(obj, ref buffer);
            using(var m1 = new MemoryStream(buffer))
            using (var m2 = new MemoryStream())
            {
                BZip2.Compress(m1, m2, false, 9);
                m2.Seek(0, SeekOrigin.Begin);
                File.WriteAllBytes(filename, m2.ToArray());
            }
        }

        public static T FromCERAS<T>(this Stream data, SerializerConfig config)
        {
            var ceras = new CerasSerializer(config);
            byte[] bytes = data.ReadAll();
            using (var m1 = new MemoryStream(bytes))
            using (var m2 = new MemoryStream())
            {
                BZip2.Decompress(m1, m2, false);
                m2.Seek(0, SeekOrigin.Begin);
                return ceras.Deserialize<T>(m2.ToArray());
            }
        }

        public static void ToJSON<T>(this T obj, string filename)
        {
            if (File.Exists(filename))
                File.Delete(filename);
            File.WriteAllText(filename,
                JsonConvert.SerializeObject(obj, Formatting.Indented,
                    new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto}));
        }
        /*
        public static void ToBSON<T>(this T obj, string filename)
        {
            using (var fo = File.OpenWrite(filename))
            using (var br = new BsonDataWriter(fo))
            {
                fo.SetLength(0);
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                    {TypeNameHandling = TypeNameHandling.Auto});
                serializer.Serialize(br, obj);
            }
        }*/

        public static ulong ToMilliseconds(this DateTime date)
        {
            return (ulong) (date - new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        public static string ToJSON<T>(this T obj, 
            TypeNameHandling handling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling format = TypeNameAssemblyFormatHandling.Full)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented,
                new JsonSerializerSettings {TypeNameHandling = handling, TypeNameAssemblyFormatHandling = format});
        }
        
        public static T FromJSON<T>(this string filename, 
            TypeNameHandling handling = TypeNameHandling.All, 
            TypeNameAssemblyFormatHandling format = TypeNameAssemblyFormatHandling.Full)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(filename),
                new JsonSerializerSettings {TypeNameHandling = handling, TypeNameAssemblyFormatHandling = format});
        }
        /*
        public static T FromBSON<T>(this string filename, bool root_is_array = false)
        {
            using (var fo = File.OpenRead(filename))
            using (var br = new BsonDataReader(fo, root_is_array, DateTimeKind.Local))
            {
                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                    {TypeNameHandling = TypeNameHandling.Auto});
                return serializer.Deserialize<T>(br);
            }
        }*/

        public static T FromJSONString<T>(this string data, 
            TypeNameHandling handling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling format = TypeNameAssemblyFormatHandling.Full)
        {
            return JsonConvert.DeserializeObject<T>(data,
                new JsonSerializerSettings {TypeNameHandling = handling, TypeNameAssemblyFormatHandling = format});
        }

        public static T FromJSON<T>(this Stream data)
        {
            var s = Encoding.UTF8.GetString(data.ReadAll());
            try
            {
                return JsonConvert.DeserializeObject<T>(s,
                    new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto});
            }
            catch (JsonSerializationException)
            {
                var error = JsonConvert.DeserializeObject<NexusErrorResponse>(s,
                    new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto});
                if (error != null)
                    Log($"Exception while deserializing\nError code: {error.code}\nError message: {error.message}");
                throw;
            }
        }

        public static bool FileExists(this string filename)
        {
            return File.Exists(filename);
        }

        public static string RelativeTo(this string file, string folder)
        {
            return file.Substring(folder.Length + 1);
        }

        /// <summary>
        ///     Returns the string compressed via BZip2
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] BZip2String(this string data)
        {
            using (var os = new MemoryStream())
            {
                using (var bz = new BZip2OutputStream(os))
                {
                    using (var bw = new BinaryWriter(bz))
                    {
                        bw.Write(data);
                    }
                }

                return os.ToArray();
            }
        }

        public static void BZip2ExtractToFile(this Stream src, string dest)
        {
            using (var os = File.OpenWrite(dest))
            {
                os.SetLength(0);
                using (var bz = new BZip2InputStream(src))
                    bz.CopyTo(os);
            }
        }

        /// <summary>
        ///     Returns the string compressed via BZip2
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string BZip2String(this byte[] data)
        {
            using (var s = new MemoryStream(data))
            {
                using (var bz = new BZip2InputStream(s))
                {
                    using (var bw = new BinaryReader(bz))
                    {
                        return bw.ReadString();
                    }
                }
            }
        }

        /// <summary>
        /// A combination of .Select(func).Where(v => v != default). So select and filter default values.
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="coll"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static IEnumerable<TOut> Keep<TIn, TOut>(this IEnumerable<TIn> coll, Func<TIn, TOut> func) where TOut : IComparable<TOut>
        {
            return coll.Select(func).Where(v => v.CompareTo(default) != 0);
        }

        public static byte[] ReadAll(this Stream ins)
        {
            using (var ms = new MemoryStream())
            {
                ins.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static async Task<TR[]> PMap<TI, TR>(this IEnumerable<TI> coll, WorkQueue queue, StatusUpdateTracker updateTracker,
            Func<TI, TR> f)
        {
            var cnt = 0;
            var collist = coll.ToList();
            return await collist.PMap(queue, itm =>
            {
                updateTracker.MakeUpdate(collist.Count, Interlocked.Increment(ref cnt));
                return f(itm);
            });
        }

        public static async Task<TR[]> PMap<TI, TR>(this IEnumerable<TI> coll, WorkQueue queue, StatusUpdateTracker updateTracker,
            Func<TI, Task<TR>> f)
        {
            var cnt = 0;
            var collist = coll.ToList();
            return await collist.PMap(queue, itm =>
            {
                updateTracker.MakeUpdate(collist.Count, Interlocked.Increment(ref cnt));
                return f(itm);
            });
        }

        public static async Task PMap<TI>(this IEnumerable<TI> coll, WorkQueue queue, StatusUpdateTracker updateTracker,
            Func<TI, Task> f)
        {
            var cnt = 0;
            var collist = coll.ToList();
            await collist.PMap(queue, async itm =>
            {
                updateTracker.MakeUpdate(collist.Count, Interlocked.Increment(ref cnt));
                await f(itm);
            });
        }

        public static async Task PMap<TI>(this IEnumerable<TI> coll, WorkQueue queue, StatusUpdateTracker updateTracker,
            Action<TI> f)
        {
            var cnt = 0;
            var collist = coll.ToList();
            await collist.PMap(queue, itm =>
            {
                updateTracker.MakeUpdate(collist.Count, Interlocked.Increment(ref cnt));
                f(itm);
                return true;
            });
        }

        public static async Task<TR[]> PMap<TI, TR>(this IEnumerable<TI> coll, WorkQueue queue,
            Func<TI, TR> f)
        {
            var colllst = coll.ToList();

            var remainingTasks = colllst.Count;

            var tasks = colllst.Select(i =>
            {
                var tc = new TaskCompletionSource<TR>();
                queue.QueueTask(async () =>
                {
                    try
                    {
                        tc.SetResult(f(i));
                    }
                    catch (Exception ex)
                    {
                        tc.SetException(ex);
                    }
                    Interlocked.Decrement(ref remainingTasks);
                });
                return tc.Task;
            }).ToList();

            // To avoid thread starvation, we'll start to help out in the work queue
            if (WorkQueue.WorkerThread)
                while (remainingTasks > 0)
                    if (queue.Queue.TryTake(out var a, 500))
                    {
                        WorkQueue.AsyncLocalCurrentQueue.Value = WorkQueue.ThreadLocalCurrentQueue.Value;
                        await a();
                    }

            return await Task.WhenAll(tasks);
        }

        public static async Task<TR[]> PMap<TI, TR>(this IEnumerable<TI> coll, WorkQueue queue,
            Func<TI, Task<TR>> f)
        {
            var colllst = coll.ToList();

            var remainingTasks = colllst.Count;

            var tasks = colllst.Select(i =>
            {
                var tc = new TaskCompletionSource<TR>();
                queue.QueueTask(async () =>
                {
                    try
                    {
                        tc.SetResult(await f(i));
                    }
                    catch (Exception ex)
                    {
                        tc.SetException(ex);
                    }
                    Interlocked.Decrement(ref remainingTasks);
                });
                return tc.Task;
            }).ToList();

            // To avoid thread starvation, we'll start to help out in the work queue
            if (WorkQueue.WorkerThread)
                while (remainingTasks > 0)
                    if (queue.Queue.TryTake(out var a, 500))
                    {
                        WorkQueue.AsyncLocalCurrentQueue.Value = WorkQueue.ThreadLocalCurrentQueue.Value;
                        await a();
                    }

            return await Task.WhenAll(tasks);
        }

        public static async Task PMap<TI>(this IEnumerable<TI> coll, WorkQueue queue,
            Func<TI, Task> f)
        {
            var colllst = coll.ToList();

            var remainingTasks = colllst.Count;

            var tasks = colllst.Select(i =>
            {
                var tc = new TaskCompletionSource<bool>();
                queue.QueueTask(async () =>
                {
                    try
                    {
                        await f(i);
                        tc.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tc.SetException(ex);
                    }
                    Interlocked.Decrement(ref remainingTasks);
                });
                return tc.Task;
            }).ToList();

            // To avoid thread starvation, we'll start to help out in the work queue
            if (WorkQueue.WorkerThread)
                while (remainingTasks > 0)
                    if (queue.Queue.TryTake(out var a, 500))
                    {
                        WorkQueue.AsyncLocalCurrentQueue.Value = WorkQueue.ThreadLocalCurrentQueue.Value;
                        await a();
                    }

            await Task.WhenAll(tasks);
        }

        public static async Task PMap<TI>(this IEnumerable<TI> coll, WorkQueue queue, Action<TI> f)
        {
            await coll.PMap(queue, i =>
            {
                f(i);
                return false;
            });
        }

        public static void DoProgress<T>(this IEnumerable<T> coll, string msg, Action<T> f)
        {
            var lst = coll.ToList();
            lst.DoIndexed((idx, i) =>
            {
                Status(msg, idx * 100 / lst.Count);
                f(i);
            });
        }

        public static void OnQueue(Action f)
        {
            new List<bool>().Do(_ => f());
        }

        public static async Task<Stream> PostStream(this HttpClient client, string url, HttpContent content)
        {
            var result = await client.PostAsync(url, content);
            return await result.Content.ReadAsStreamAsync();
        }

        public static IEnumerable<T> DistinctBy<T, V>(this IEnumerable<T> vs, Func<T, V> select)
        {
            var set = new HashSet<V>();
            foreach (var v in vs)
            {
                var key = select(v);
                if (set.Contains(key)) continue;
                yield return v;
            }
        }

        public static T Last<T>(this T[] a)
        {
            if (a == null || a.Length == 0)
                throw new InvalidDataException("null or empty array");
            return a[a.Length - 1];
        }

        public static V GetOrDefault<K, V>(this IDictionary<K, V> dict, K key)
        {
            if (dict.TryGetValue(key, out var v)) return v;
            return default;
        }

        public static string ToFileSizeString(this long byteCount)
        {
            if (byteCount == 0)
                return "0" + Suffix[0];
            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return Math.Sign(byteCount) * num + Suffix[place];
        }

        public static string ToFileSizeString(this int byteCount)
        {
            return ToFileSizeString((long)byteCount);
        }

        public static void CreatePatch(byte[] a, byte[] b, Stream output)
        {
            var dataA = a.xxHash().FromBase64().ToHex();
            var dataB = b.xxHash().FromBase64().ToHex();
            var cacheFile = Path.Combine("patch_cache", $"{dataA}_{dataB}.patch");
            if (!Directory.Exists("patch_cache"))
                Directory.CreateDirectory("patch_cache");

            while (true)
            {
                if (File.Exists(cacheFile))
                {
                    using (var f = File.OpenRead(cacheFile))
                    {
                        f.CopyTo(output);
                    }
                }
                else
                {
                    var tmpName = Path.Combine("patch_cache", Guid.NewGuid() + ".tmp");

                    using (var f = File.OpenWrite(tmpName))
                    {
                        Status("Creating Patch");
                        BSDiff.Create(a, b, f);
                    }

                    File.Move(tmpName, cacheFile, MoveOptions.ReplaceExisting);
                    continue;
                }

                break;
            }
        }

        public static bool TryGetPatch(string foundHash, string fileHash, out byte[] ePatch)
        {
            var patchName = Path.Combine("patch_cache",
                $"{foundHash.FromBase64().ToHex()}_{fileHash.FromBase64().ToHex()}.patch");
            if (File.Exists(patchName))
            {
                ePatch = File.ReadAllBytes(patchName);
                return true;
            }

            ePatch = null;
            return false;
        }

        /*
        public static void Warning(string s)
        {
            Log($"WARNING: {s}");
        }*/

        public static TV GetOrDefault<TK, TV>(this Dictionary<TK, TV> dict, TK key)
        {
            return dict.TryGetValue(key, out var result) ? result : default;
        }

        public static IEnumerable<T> ButLast<T>(this IEnumerable<T> coll)
        {
            var lst = coll.ToList();
            return lst.Take(lst.Count() - 1);
        }

        public static byte[] ConcatArrays(this IEnumerable<byte[]> arrays)
        {
            var outarr = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (var arr in arrays)
            {
                Array.Copy(arr, 0, outarr, offset, arr.Length);
                offset += arr.Length;
            }
            return outarr;
        }

        /// <summary>
        /// Roundtrips the value throught the JSON routines
        /// </summary>
        /// <typeparam name="TV"></typeparam>
        /// <typeparam name="TR"></typeparam>
        /// <param name="tv"></param>
        /// <returns></returns>
        public static T ViaJSON<T>(this T tv)
        {
            return tv.ToJSON().FromJSONString<T>();
        }

        /*
        public static void Error(string msg)
        {
            Log(msg);
            throw new Exception(msg);
        }*/

        public static Stream GetEmbeddedResourceStream(string name)
        {
            return (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                    where !assembly.IsDynamic
                    from rname in assembly.GetManifestResourceNames()
                    where rname == name
                    select assembly.GetManifestResourceStream(name)).First();
        }

        public static T FromYaml<T>(this Stream s)
        {
            var d = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
            return d.Deserialize<T>(new StreamReader(s));
        }

        public static T FromYaml<T>(this string s)
        {
            var d = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();
            return d.Deserialize<T>(new StringReader(s));
        }
        public static void LogStatus(string s)
        {
            Status(s);
            Log(s);
        }

        private static async Task<long> TestDiskSpeedInner(WorkQueue queue, string path)
        {
            var startTime = DateTime.Now;
            var seconds = 2;
            var results = await Enumerable.Range(0, queue.ThreadCount)
                .PMap(queue, idx =>
                {
                    var random = new Random();

                    var file = Path.Combine(path, $"size_test{idx}.bin");
                    long size = 0;
                    byte[] buffer = new byte[1024 * 8];
                    random.NextBytes(buffer);
                    using (var fs = File.OpenWrite(file))
                    {
                        while (DateTime.Now < startTime + new TimeSpan(0, 0, seconds))
                        {
                            fs.Write(buffer, 0, buffer.Length);
                            // Flush to make sure large buffers don't cause the rate to be higher than it should
                            fs.Flush();
                            size += buffer.Length;
                        }
                    }
                    File.Delete(file);
                    return size;
                });
            return results.Sum() / seconds;
        }

        private static Dictionary<string, long> _cachedDiskSpeeds = new Dictionary<string, long>();
        public static async Task<long> TestDiskSpeed(WorkQueue queue, string path)
        {
            if (_cachedDiskSpeeds.TryGetValue(path, out long speed))
                return speed;
            speed = await TestDiskSpeedInner(queue, path);
            _cachedDiskSpeeds[path] = speed;
            return speed;
        }

        /// https://stackoverflow.com/questions/422090/in-c-sharp-check-that-filename-is-possibly-valid-not-that-it-exists
        public static IErrorResponse IsFilePathValid(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ErrorResponse.Fail("Path is empty.");
            }
            try
            {
                var fi = new System.IO.FileInfo(path);
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse.Fail(ex.Message);
            }
            catch (PathTooLongException ex)
            {
                return ErrorResponse.Fail(ex.Message);
            }
            catch (NotSupportedException ex)
            {
                return ErrorResponse.Fail(ex.Message);
            }
            return ErrorResponse.Success;
        }

        public static IErrorResponse IsDirectoryPathValid(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ErrorResponse.Fail("Path is empty");
            }
            try
            {
                var fi = new System.IO.DirectoryInfo(path);
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse.Fail(ex.Message);
            }
            catch (PathTooLongException ex)
            {
                return ErrorResponse.Fail(ex.Message);
            }
            catch (NotSupportedException ex)
            {
                return ErrorResponse.Fail(ex.Message);
            }
            return ErrorResponse.Success;
        }

        /// <summary>
        /// Both AlphaFS and C#'s Directory.Delete sometimes fail when certain files are read-only
        /// or have other weird attributes. This is the only 100% reliable way I've found to completely
        /// delete a folder. If you don't like this code, it's unlikely to change without a ton of testing.
        /// </summary>
        /// <param name="path"></param>
        public static void DeleteDirectory(string path)
        {
            var info = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c del /f /q /s \"{path}\" && rmdir /q /s \"{path}\" ",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = new Process
            {
                StartInfo = info
            };

            p.Start();
            ChildProcessTracker.AddProcess(p);
            try
            {
                p.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception)
            {
            }

            while (!p.HasExited)
            {
                var line = p.StandardOutput.ReadLine();
                if (line == null) break;
                Status(line);
            }
            p.WaitForExitAndWarn(TimeSpan.FromSeconds(30), $"Deletion process of {path}");
        }

        public static bool IsUnderneathDirectory(string path, string dirPath)
        {
            return path.StartsWith(dirPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes a file to JSON but in an encrypted format in the user's app local directory.
        /// The data will be encrypted so that it can only be read by this machine and this user.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="data"></param>
        public static void ToEcryptedJson<T>(this T data, string key)
        {
            var bytes = Encoding.UTF8.GetBytes(data.ToJSON());
            var encoded = ProtectedData.Protect(bytes, Encoding.UTF8.GetBytes(key), DataProtectionScope.LocalMachine);
            
            if (!Directory.Exists(Consts.LocalAppDataPath))
                Directory.CreateDirectory(Consts.LocalAppDataPath);
            
            var path = Path.Combine(Consts.LocalAppDataPath, key);
            File.WriteAllBytes(path, encoded);
        }

        public static T FromEncryptedJson<T>(string key)
        {
            var path = Path.Combine(Consts.LocalAppDataPath, key);
            var bytes = File.ReadAllBytes(path);
            var decoded = ProtectedData.Unprotect(bytes, Encoding.UTF8.GetBytes(key), DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(decoded).FromJSONString<T>();
        }

        public static bool HaveEncryptedJson(string key)
        {
            var path = Path.Combine(Consts.LocalAppDataPath, key);
            return File.Exists(path);
        }

        public static IObservable<(FileEventType, FileSystemEventArgs)> AppLocalEvents { get; }

        public static IObservable<bool> HaveEncryptedJsonObservable(string key)
        {
            var path = Path.Combine(Consts.LocalAppDataPath, key).ToLower();
            return AppLocalEvents.Where(t => t.Item2.FullPath.ToLower() == path)
                                 .Select(_ => File.Exists(path))
                                 .StartWith(File.Exists(path))
                                 .DistinctUntilChanged();
        }

        public static void DeleteEncryptedJson(string key)
        {
            var path = Path.Combine(Consts.LocalAppDataPath, key);
            if (File.Exists(path))
                File.Delete(path);
        }


        public static bool IsInPath(this string path, string parent)
        {
            return path.ToLower().TrimEnd('\\').StartsWith(parent.ToLower().TrimEnd('\\') + "\\");
        }

        public class NexusErrorResponse
        {
            public int code;
            public string message;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }


        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static MEMORYSTATUSEX GetMemoryStatus()
        {
            var mstat = new MEMORYSTATUSEX();
            GlobalMemoryStatusEx(mstat);
            return mstat;
        }
    }
}
