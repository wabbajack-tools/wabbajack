using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
 
    public class LoginManagerVM : BackNavigatingVM
    {
        public List<LoginTargetVM> Downloaders { get; }

        public LoginManagerVM(ILogger<LoginManagerVM> logger, SettingsVM settingsVM)
            : base(logger)
        {
            /*
            Downloaders = DownloadDispatcher.Downloaders
                .OfType<INeedsLogin>()
                .OrderBy(x => x.SiteName)
                .Select(x => new LoginTargetVM(x))
                .ToList();
                */
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

            /*
            _metaInfo = (login.MetaInfo ?? Observable.Return(""))
                .ToGuiProperty(this, nameof(MetaInfo));*/

            if (!UsesCredentials)
                return;
/*
            TriggerCredentialsLogin = ReactiveCommand.Create(() =>
            {
                if (!(login is INeedsLoginCredentials))
                    return;

                var loginWindow = new LoginWindowView(LoginWithCredentials);
                loginWindow.Show();
            }, LoginWithCredentials.IsLoggedIn.Select(b => !b).ObserveOnGuiThread());
            */
        }

        private void LoadImage()
        {
            /*
            Task.Run(async () =>
            {
                if (Login.IconUri == null) return;

                if(!Consts.FaviconCacheFolderPath.Exists)
                    Consts.FaviconCacheFolderPath.CreateDirectory();

                var faviconIcon = Consts.FaviconCacheFolderPath.Combine($"{Login.SiteName}.ico");
                if (faviconIcon.Exists)
                {
                    var fsi = new FileInfo(faviconIcon.ToString());
                    var creationDate = fsi.CreationTimeUtc;
                    var now = DateTime.UtcNow;

                    //delete favicons older than 10 days

                    if ((now - creationDate).TotalDays > 10)
                        await faviconIcon.DeleteAsync();
                }

                if (faviconIcon.Exists)
                {
                    await using var fs = await faviconIcon.OpenRead();

                    var ms = new MemoryStream((int)fs.Length);
                    await fs.CopyToAsync(ms);
                    ms.Position = 0;

                    var source = new BitmapImage();
                    source.BeginInit();
                    source.StreamSource = ms;
                    source.EndInit();
                    source.Freeze();
                    Favicon = source;
                }
                else
                {
                    using var img = await new Lib.Http.Client().GetAsync(Login.IconUri, errorsAsExceptions: false);
                    if (!img.IsSuccessStatusCode) return;

                    var icoData = new MemoryStream(await img.Content.ReadAsByteArrayAsync());

                    var data = new Icon(icoData);
                    var ms = new MemoryStream();
                    data.ToBitmap().Save(ms, ImageFormat.Png);
                    ms.Position = 0;

                    await using (var fs = await faviconIcon.Create())
                    {
                        await ms.CopyToAsync(fs);
                        ms.Position = 0;
                    }

                    var source = new BitmapImage();
                    source.BeginInit();
                    source.StreamSource = ms;
                    source.EndInit();
                    source.Freeze();
                    Favicon = source;
                }
            });
            */
        }
    }
    
}
