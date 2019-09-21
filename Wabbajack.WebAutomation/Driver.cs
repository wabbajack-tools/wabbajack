using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.IE;
using Wabbajack.Common;

namespace Wabbajack.WebAutomation
{
    public class Driver
    {
        public static void Init()
        {
            Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .Where(s => s.StartsWith("Wabbajack.WebAutomation."))
                .Do(s =>
                {
                    var filename = s.Substring("Wabbajack.WebAutomation.".Length);

                    try
                    {
                        using (var fs = File.OpenWrite(filename))
                        {
                            fs.SetLength(0);
                            Assembly.GetExecutingAssembly()
                                .GetManifestResourceStream(s)
                                .CopyTo(fs);
                        }
                    }
                    catch (IOException)
                    {
                        // Ignore
                    }
                });
        }

        public static IWebDriver GetDriver()
        {
            if (_foundDriver == null || _foundDriver == DriverType.Chrome)
            {
                try
                {
                    var service = ChromeDriverService.CreateDefaultService();
                    service.HideCommandPromptWindow = true;
                    
                    var options = new ChromeOptions();
                    options.AddArguments(new List<string>() {
                        "--silent-launch",
                        "--no-startup-window",
                        "no-sandbox",
                        "headless",});


                    var driver = new ChromeDriver(service, options);

                    ChildProcessTracker.AddProcess(Process.GetProcesses().Single(p => p.Id == service.ProcessId));

                    _foundDriver = DriverType.Chrome; 
                    return driver;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            if (_foundDriver == null || _foundDriver == DriverType.InternetExplorer)
            {
                try
                {
                    var driver = new InternetExplorerDriver();
                    _foundDriver = DriverType.InternetExplorer;
                    return driver;

                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return null;
        }

        private static Object _lockObject = new object();

        public enum DriverType
        {
            Chrome,
            Firefox,
            InternetExplorer
        };

        private static DriverType? _foundDriver;
    }
}
