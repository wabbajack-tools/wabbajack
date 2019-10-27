using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Wabbajack.Lib.WebAutomation
{
    public enum DisplayMode
    {
        Visible,
        Hidden
    }

    public class Driver : IDisposable
    {
        private WebAutomationWindow _window;
        private WebAutomationWindowViewModel _ctx;
        private Task<WebAutomationWindow> _windowTask;

        private Driver(DisplayMode displayMode = DisplayMode.Hidden)
        {
            var windowTask = new TaskCompletionSource<WebAutomationWindow>();

            var t = new Thread(() =>
            {
                _window = new WebAutomationWindow();
                _ctx = (WebAutomationWindowViewModel)_window.DataContext;
                // Initiates the dispatcher thread shutdown when the window closes
                    
                _window.Closed += (s, e) => _window.Dispatcher.InvokeShutdown();

                if (displayMode == DisplayMode.Hidden)
                {
                    _window.WindowState = WindowState.Minimized;
                    _window.ShowInTaskbar = false;
                }

                _window.Show();

                windowTask.SetResult(_window);
                // Makes the thread support message pumping
                System.Windows.Threading.Dispatcher.Run();
            });
            _windowTask = windowTask.Task;

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        public static async Task<Driver> Create()
        {
            var driver = new Driver();
            driver._window = await driver._windowTask;
            driver._ctx = (WebAutomationWindowViewModel) driver._window.DataContext;
            return driver;
        }

        public Task<Uri> NavigateTo(Uri uri)
        {
            return _ctx.NavigateTo(uri);
        }

        public Task<Uri> GetLocation()
        {
            var tcs = new TaskCompletionSource<Uri>();
            _window.Dispatcher.Invoke(() => tcs.SetResult(_window.WebView.Source));
            return tcs.Task;
        }

        public Task<string> GetAttr(string selector, string attr)
        {
            var tcs = new TaskCompletionSource<string>();
            _window.Dispatcher.Invoke(() =>
            {
                try
                {
                    var script = $"document.querySelector(\"{selector}\").{attr}";
                    var result = _window.WebView.InvokeScript("eval", script);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public void Dispose()
        {
            _window.Dispatcher.Invoke(_window.Close);
        }
    }
}
