using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.WebAutomation.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Utils.SetLoggerFn(Console.WriteLine);
            Utils.SetStatusFn((msg, i) =>
            {
                if (i != 0)
                    Console.WriteLine($"{i}% - {msg}");
                else
                    Console.WriteLine($"{msg}");

            });

            Driver.Init();

            var hook = HookRegistry.FindSiteHook();
            var site = "http://www.mediafire.com/file/x7dgot1r3z1u8p8/CreationKit_1_5_3.7z";

            hook.Download(null, new Dictionary<string, string>() {{"mediaFireURL", site}}, @"c:\tmp\out.7z");
        }
    }
}
