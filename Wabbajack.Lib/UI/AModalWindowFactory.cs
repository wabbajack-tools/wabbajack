using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Wabbajack.Lib.UI
{
    public abstract class AModalWindowFactory
    {
        public static AModalWindowFactory CurrentFactory = new DefaultModalWindowFactory();
        public abstract Task<object> Show(InlinedWindowVM vm);
        public abstract Task SetResult(object result);
        public abstract Task Cancel();
    }

    public class DefaultModalWindowFactory : AModalWindowFactory
    {
        private Window _window;
        private bool _isShown;
        private TaskCompletionSource<object> _tcs;

        public override Task<object> Show(InlinedWindowVM vm)
        {

            lock (this)
            {
                if (_isShown) 
                    throw new InvalidDataException("Can't show a window twice without first closing");
                _tcs = new TaskCompletionSource<object>();
                
                _window = new Window {Content = vm.Content};
                _window.DataContext = vm;
                _isShown = true;
                _window.Show();
                _window.Closed += Window_Closed;

                return _tcs.Task;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Cancel();
        }

        public override async Task SetResult(object result)
        {
            await _window.Dispatcher.InvokeAsync(() =>
            {
                lock (this)
                {
                    if (_isShown)
                        _tcs.SetResult(result);
                    _isShown = false;
                    _window.Close();
                }
            });
        }

        public override async Task Cancel()
        {
            await _window.Dispatcher.InvokeAsync(() =>
            {
                lock (this)
                {
                    if (!_isShown)
                    {
                        return;
                    }

                    _isShown = false;
                    _tcs.SetCanceled();
                }
            });
        }
    }
    
}
