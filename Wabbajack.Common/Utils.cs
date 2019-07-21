using IniParser;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public static T FromJSON<T>(this string filename)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(filename));
        }

        public static bool FileExists(this string filename)
        {
            return File.Exists(filename);
        }

        public static string RelativeTo(this string file, string folder)
        {
            return file.Substring(folder.Length + 1);
        }

    }
}
