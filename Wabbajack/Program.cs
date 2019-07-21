using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack
{
    class Program
    {
        static void Main(string[] args)
        {
            var compiler = new Compiler("c:\\Mod Organizer 2", msg => Console.WriteLine(msg), (msg, id, prog) => Console.WriteLine(msg));
            compiler.LoadArchives();
            compiler.Compile();

        }
    }
}
