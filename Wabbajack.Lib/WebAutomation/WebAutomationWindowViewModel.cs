using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT;
using Microsoft.Toolkit.Wpf.UI.Controls;

namespace Wabbajack.Lib.WebAutomation
{
    public class WebAutomationWindowViewModel : ViewModel
    {
        private WebAutomationWindow _window;

        private WebView Browser => _window.WebView;

        public WebAutomationWindowViewModel(WebAutomationWindow window)
        {
            _window = window;
        }

        public Task<Uri> NavigateTo(Uri uri)
        {
            var tcs = new TaskCompletionSource<Uri>();

            EventHandler<WebViewControlNavigationCompletedEventArgs> handler = null;
            handler = (s, e) =>
            {
                Browser.NavigationCompleted -= handler;
                tcs.SetResult(uri);
            };
            Browser.NavigationCompleted += handler;
            Browser.Source = uri;
            return tcs.Task;
        }


    }
}
