using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Wabbajack.Lib.UI.ModalWindows
{
    public class ModalMessageBox : InlinedWindowVM
    {
        public BoxType Type { get; }
        public string Message { get; }
        public string Title { get; }

        public enum BoxType
        {
            OkCancel,
        }

        public enum Result
        {
            Ok,
            Cancel
        }

        public ModalMessageBox(string title, string message, BoxType type)
        {
            Title = title;
            Type = type;
            Message = message;
            if (type == BoxType.OkCancel) Content = new ModalMessageBoxUI {DataContext = this};

        }

        public static async Task<Result> OkCancel(string title, string message)
        {
            var res = await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    return (Result)await AModalWindowFactory.CurrentFactory.Show(
                        new ModalMessageBox(title, message, BoxType.OkCancel));
                }
                catch (Exception ex)
                {
                    return Result.Cancel;
                }
            });
            return await res;

        }
    }
}
