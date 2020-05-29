using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack
{
    public class LoginManagerVM : BackNavigatingVM
    {
        public List<LoginTargetVM> Downloaders { get; }

        public LoginManagerVM(SettingsVM settingsVM)
            : base(settingsVM.MWVM)
        {
            Downloaders = DownloadDispatcher.Downloaders
                .OfType<INeedsLogin>()
                .OrderBy(x => x.SiteName)
                .Select(x => new LoginTargetVM(x))
                .ToList();
        }
    }

    public class LoginTargetVM : ViewModel
    {
        private readonly ObservableAsPropertyHelper<string> _metaInfo;
        public string MetaInfo => _metaInfo.Value;

        public INeedsLogin Login { get; }
        public INeedsLoginCredentials LoginWithCredentials { get; }
        public bool UsesCredentials { get; }

        public ReactiveCommand<Unit, Unit> TriggerCredentialsLogin;

        private ImageSource _favicon = null;
        
        public ImageSource Favicon { get => _favicon; set => RaiseAndSetIfChanged(ref _favicon, value); }

        public LoginTargetVM(INeedsLogin login)
        {
            Login = login;

            LoadImage();

            if (login is INeedsLoginCredentials loginWithCredentials)
            {
                UsesCredentials = true;
                LoginWithCredentials = loginWithCredentials;
            }

            _metaInfo = (login.MetaInfo ?? Observable.Return(""))
                .ToGuiProperty(this, nameof(MetaInfo));

            if (!UsesCredentials)
                return;

            TriggerCredentialsLogin = ReactiveCommand.Create(() =>
            {
                if (!(login is INeedsLoginCredentials))
                    return;

                var loginWindow = new LoginWindowView(LoginWithCredentials);
                loginWindow.Show();
            }, LoginWithCredentials.IsLoggedIn.Select(b => !b).ObserveOnGuiThread());
        }

        private void LoadImage()
        {
            Task.Run(async () =>
            {
                if (Login.IconUri == null) return;

                using var img = await new Common.Http.Client().GetAsync(Login.IconUri, errorsAsExceptions:false);
                if (!img.IsSuccessStatusCode) return;

                var icoData = new MemoryStream(await img.Content.ReadAsByteArrayAsync());
                
                var data = new Icon(icoData);
                var ms = new MemoryStream(); 
                data.ToBitmap().Save(ms, ImageFormat.Png);
                ms.Position = 0;
                
                var source = new BitmapImage();
                source.BeginInit();
                source.StreamSource = ms;
                source.EndInit();
                source.Freeze();
                Favicon = source;
            });
        }
    }
}
