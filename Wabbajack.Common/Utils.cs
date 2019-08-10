using ICSharpCode.SharpZipLib.BZip2;
using IniParser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public static class Utils
    {

      

        /// <summary>
        /// MurMur3 hashes the file pointed to by this string
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string FileSHA256(this string file)
        {
            var sha = new SHA256Managed();
            using (var o = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            {
                using (var i = File.OpenRead(file))
                    i.CopyTo(o);
            }
            return sha.Hash.ToBase64();

        }

        public static string SHA256(this byte[] data)
        {
            return new SHA256Managed().ComputeHash(data).ToBase64();

        }

        /// <summary>
        /// Returns a Base64 encoding of these bytes
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToBase64(this byte[] data)
        {
            return Convert.ToBase64String(data);
        }

        /// <summary>
        /// Returns data from a base64 stream
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] FromBase64(this string data)
        {
            return Convert.FromBase64String(data);
        }

        /// <summary>
        /// Executes the action for every item in coll
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="coll"></param>
        /// <param name="f"></param>
        public static void Do<T>(this IEnumerable<T> coll, Action<T> f)
        {
            foreach (var i in coll) f(i);
        }

        /// <summary>
        /// Loads INI data from the given filename and returns a dynamic type that
        /// can use . operators to navigate the INI.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static dynamic LoadIniFile(this string file)
        {
            return new DynamicIniData(new FileIniDataParser().ReadFile(file));
        }

        public static void ToJSON<T>(this T obj, string filename)
        {
            File.WriteAllText(filename, JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings() {TypeNameHandling = TypeNameHandling.Auto}));
        }

        public static string ToJSON<T>(this T obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto });
        }

        public static T FromJSON<T>(this string filename)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(filename), new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto });
        }

        public static T FromJSONString<T>(this string data)
        {
            return JsonConvert.DeserializeObject<T>(data, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto });
        }
        public static T FromJSON<T>(this Stream data)
        {
            var s = Encoding.UTF8.GetString(data.ReadAll());
            return JsonConvert.DeserializeObject<T>(s);
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
        /// Returns the string compressed via BZip2
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
                        bw.Write(data);
                }
                return os.ToArray();
            }
        }

        /// <summary>
        /// Returns the string compressed via BZip2
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
                        return bw.ReadString();
                }
            }
        }

        public static byte[] ReadAll(this Stream ins)
        {
            using (var ms = new MemoryStream())
            {
                ins.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static List<TR> PMap<TI, TR>(this IEnumerable<TI> coll, Func<TI, TR> f)
        {
            var colllst = coll.ToList();
            WorkQueue.MaxQueueSize = colllst.Count;
            WorkQueue.CurrentQueueSize = 0;

            int remaining_tasks = colllst.Count;

            var tasks = coll.Select(i =>
            {
                TaskCompletionSource<TR> tc = new TaskCompletionSource<TR>();
                WorkQueue.QueueTask(() =>
                {
                    try
                    {
                        tc.SetResult(f(i));
                    }
                    catch (Exception ex)
                    {
                        tc.SetException(ex);
                    }
                    Interlocked.Increment(ref WorkQueue.CurrentQueueSize);
                    Interlocked.Decrement(ref remaining_tasks);
                    WorkQueue.ReportNow();
                });
                return tc.Task;
            }).ToList();

            // To avoid thread starvation, we'll start to help out in the work queue
            if (WorkQueue.WorkerThread)
            while(remaining_tasks > 0)
            {
                if(WorkQueue.Queue.TryTake(out var a, 500))
                {
                    a();
                }
            }

            return tasks.Select(t =>
            {
                t.Wait();
                if (t.IsFaulted)
                    throw t.Exception;
                return t.Result;
            }).ToList();
        }

        public static void PMap<TI>(this IEnumerable<TI> coll, Action<TI> f)
        {
            coll.PMap<TI, bool>(i =>
            {
                f(i);
                return false;
            });
            return;
        }

        public static HttpResponseMessage GetSync(this HttpClient client, string url)
        {
            var result = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            result.Wait();
            return result.Result;
        }
        public static string GetStringSync(this HttpClient client, string url)
        {
            var result = client.GetStringAsync(url);
            result.Wait();
            return result.Result;
        }

        public static Stream GetStreamSync(this HttpClient client, string url)
        {
            var result = client.GetStreamAsync(url);
            result.Wait();
            return result.Result;
        }

        public static string ExceptionToString(this Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            while (ex != null)
            {
                sb.AppendLine(ex.Message);
                var st = new StackTrace(ex, true);
                foreach (var frame in st.GetFrames())
                {
                    sb.AppendLine($"{frame.GetFileName()}:{frame.GetMethod().Name}:{frame.GetFileLineNumber()}:{frame.GetFileColumnNumber()}");
                }
                ex = ex.InnerException;
            }


            return sb.ToString();
        }

        public static void CrashDump(Exception e)
        {
            File.WriteAllText($"{DateTime.Now.ToString("yyyyMMddTHHmmss_crash_log.txt")}", ExceptionToString(e));
        }

        public static V GetOrDefault<K, V>(this IDictionary<K, V> dict, K key)
        {
            if (dict.TryGetValue(key, out V v)) return v;
            return default(V);
        }

    }
}
