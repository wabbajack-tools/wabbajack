using DynamicData;
using DynamicData.Binding;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Wabbajack.Common;

namespace Wabbajack.UI
{
    public static class UIUtils
    {
        public static BitmapImage BitmapImageFromResource(string name) => BitmapImageFromStream(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Wabbajack;component/" + name)).Stream);

        public static BitmapImage BitmapImageFromStream(Stream stream)
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = stream;
            img.EndInit();
            img.Freeze();
            return img;
        }

        public static bool TryGetBitmapImageFromFile(string path, out BitmapImage bitmapImage)
        {
            try
            {
                if (!File.Exists(path))
                {
                    bitmapImage = default;
                    return false;
                }
                bitmapImage = new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
                return true;
            }
            catch (Exception)
            {
                bitmapImage = default;
                return false;
            }
        }

        public static string OpenFileDialog(string filter, string initialDirectory = null)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = filter;
            ofd.InitialDirectory = initialDirectory;
            if (ofd.ShowDialog() == DialogResult.OK)
                return ofd.FileName;
            return null;
        }

        public static IDisposable BindCpuStatus(IObservable<CPUStatus> status, ObservableCollectionExtended<CPUDisplayVM> list)
        {
            return status.ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet(x => x.ID)
                .Batch(TimeSpan.FromMilliseconds(50), RxApp.TaskpoolScheduler)
                .EnsureUniqueChanges()
                .TransformAndCache(
                    onAdded: (key, cpu) => new CPUDisplayVM(cpu),
                    onUpdated: (change, vm) => vm.AbsorbStatus(change.Current))
                .AutoRefresh(x => x.IsWorking)
                .AutoRefresh(x => x.StartTime)
                .Filter(i => i.IsWorking && i.ID != WorkQueue.UnassignedCpuId)
                .Sort(SortExpressionComparer<CPUDisplayVM>.Ascending(s => s.StartTime))
                .ObserveOnGuiThread()
                .Bind(list)
                .Subscribe();
        }
    }
}
