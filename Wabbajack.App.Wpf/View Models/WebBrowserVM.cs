using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack
{
    public class WebBrowserVM : ViewModel, IBackNavigatingVM, IDisposable
    {
        private readonly ILogger _logger;

        [Reactive]
        public string Instructions { get; set; }

        public IWebBrowser Browser { get; } = new ChromiumWebBrowser();
        public CefSharpWrapper Driver => new(_logger, Browser);

        [Reactive]
        public ViewModel NavigateBackTarget { get; set; }

        [Reactive]
        public ReactiveCommand<Unit, Unit> BackCommand { get; set; }

        public Subject<bool> IsBackEnabledSubject { get; } = new Subject<bool>();
        public IObservable<bool> IsBackEnabled { get; }

        private WebBrowserVM(ILogger logger, string url = "http://www.wabbajack.org")
        {
            _logger = logger;
            IsBackEnabled = IsBackEnabledSubject.StartWith(true);
            Instructions = "Wabbajack Web Browser";
        }

        public static async Task<WebBrowserVM> GetNew(ILogger logger, string url = "http://www.wabbajack.org")
        {
            // Make sure libraries are extracted first
            return new WebBrowserVM(logger, url);
        }

        public override void Dispose()
        {
            Browser.Dispose();
            base.Dispose();
        }
    }
}
