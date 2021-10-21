using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.DownloadStates;

namespace Wabbajack.App.ViewModels.SubViewModels
{
    public class SlideViewModel : ViewModelBase
    {
        [Reactive]
        public IMetaState MetaState { get; set; }
        
        [Reactive]
        public IImage? Image { get; set; }

        public bool Loading { get; set; } = false;

        public SlideViewModel()
        {
            Activator = new ViewModelActivator();
            Image = null;
        }

        public async Task PreCache(HttpClient client)
        {
            Loading = true;
            var url = await client.GetByteArrayAsync(MetaState.ImageURL);
            var img = new Bitmap(new MemoryStream(url));
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Image = img;
            });
            
            Loading = false;
        }

    }
}