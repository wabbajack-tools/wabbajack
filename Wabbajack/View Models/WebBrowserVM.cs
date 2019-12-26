using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.WebAutomation;
using Xilium.CefGlue.WPF;

namespace Wabbajack
{
    public class WebBrowserVM : ViewModel
    {
        [Reactive]
        public string Instructions { get; set; }

        public IWebBrowser Browser { get; } = new ChromiumWebBrowser();
        public CefSharpWrapper Driver => new CefSharpWrapper(Browser);

        [Reactive]
        public IReactiveCommand BackCommand { get; set; }

        private WebBrowserVM(string url = "http://www.wabbajack.org")
        {
            Instructions = "Wabbajack Web Browser";
        }

        public static async Task<WebBrowserVM> GetNew(string url = "http://www.wabbajack.org")
        {
            // Make sure libraries are extracted first
            await Helpers.Initialize();
            return new WebBrowserVM(url);
        }
    }
}
