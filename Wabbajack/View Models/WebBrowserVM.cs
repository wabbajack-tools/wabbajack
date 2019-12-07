using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.WPF;

namespace Wabbajack
{
    public class WebBrowserVM : ViewModel
    {
        [Reactive]
        public string Address { get; set; }

        [Reactive]
        public string Instructions { get; set; }

        public BaseCefBrowser Browser { get; internal set; }

        public WebBrowserVM()
        {
            Instructions = "Wabbajack Web Browser";
        }
    }
}
