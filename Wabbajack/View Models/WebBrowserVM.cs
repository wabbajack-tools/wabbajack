using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Lib.LibCefHelpers;
using Xilium.CefGlue.WPF;

namespace Wabbajack
{
    public class WebBrowserVM : ViewModel
    {
        [Reactive]
        public string Instructions { get; set; }

        public WpfCefBrowser Browser { get; } = new WpfCefBrowser();

        [Reactive]
        public IReactiveCommand BackCommand { get; set; }

        private WebBrowserVM(string url = "http://www.wabbajack.org")
        {
            Browser.Address = url;
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
