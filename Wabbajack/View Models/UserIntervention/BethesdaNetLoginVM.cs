using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack
{
    public class BethesdaNetLoginVM : ViewModel, IBackNavigatingVM
    {
        [Reactive]
        public string Instructions { get; set; }

        [Reactive]
        public ViewModel NavigateBackTarget { get; set; }

        [Reactive]
        public ReactiveCommand<Unit, Unit> BackCommand { get; set; }
        
        public ReactiveCommand<Unit, Unit> LoginViaSkyrimSE { get; }
        public ReactiveCommand<Unit, Unit> LoginViaFallout4 { get; }
        
        private Subject<bool> LoggingIn = new Subject<bool>();

        private BethesdaNetLoginVM()
        {
            Instructions = "Login to Bethesda.NET in-game...";
            LoginViaSkyrimSE = ReactiveCommand.CreateFromTask(async () =>
            {
                LoggingIn.OnNext(true);
                Instructions = "Starting Skyrim Special Edition...";
                await BethesdaNetDownloader.Login(Game.SkyrimSpecialEdition);
                LoggingIn.OnNext(false);
                await BackCommand.Execute();
            }, Game.SkyrimSpecialEdition.MetaData().IsInstalled 
                ? LoggingIn.Select(e => !e).StartWith(true)
                : Observable.Return(false));
            
            LoginViaFallout4 = ReactiveCommand.CreateFromTask(async () =>
            {
                LoggingIn.OnNext(true);
                Instructions = "Starting Fallout 4...";
                await BethesdaNetDownloader.Login(Game.Fallout4);
                LoggingIn.OnNext(false);
                await BackCommand.Execute();
            }, Game.Fallout4.MetaData().IsInstalled 
                ? LoggingIn.Select(e => !e).StartWith(true)
                : Observable.Return(false));
        }

        public static async Task<BethesdaNetLoginVM> GetNew()
        {
            // Make sure libraries are extracted first
            return new BethesdaNetLoginVM();
        }
    }
}
