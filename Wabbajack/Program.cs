using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack
{
    class Program
    {
        static void Main(string[] args)
        {
            var modpack = CheckForModPack();
            if (modpack != null)
            {
                Console.WriteLine(modpack);
                Thread.Sleep(10000);
                return;
            }

            var compiler = new Compiler("c:\\Mod Organizer 2", msg => Console.WriteLine(msg), (msg, id, prog) => Console.WriteLine(msg));
            compiler.LoadArchives();
            compiler.MO2Profile = "Lexy's Legacy of The Dragonborn Special Edition";
            compiler.Compile();
            compiler.PatchExecutable();

        }

        private static string CheckForModPack()
        {
            using (var s = File.OpenRead(Assembly.GetExecutingAssembly().Location))
            {
                var magic_bytes = Encoding.ASCII.GetBytes(Consts.ModPackMagic);
                s.Position = s.Length - magic_bytes.Length;
                using (var br = new BinaryReader(s))
                {
                    var bytes = br.ReadBytes(magic_bytes.Length);
                    var magic = Encoding.ASCII.GetString(bytes);
                    if (magic != Consts.ModPackMagic)
                    {
                        return null;
                    }

                    s.Position = s.Length - magic_bytes.Length - 8;
                    var start_pos = br.ReadInt64();
                    s.Position = start_pos;
                    long length = br.ReadInt64();
                  
                    return br.ReadBytes((int)length).BZip2String();

                }
            }
        }
    }
}
