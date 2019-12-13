using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
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

        public WebBrowserVM(string url = "http://www.wabbajack.org")
        {
            Browser.Address = url;
            Instructions = "Wabbajack Web Browser";
        }
    }
}
