using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack;
using Wabbajack.Messages;
using Wabbajack.Paths.IO;
using Wabbajack.Networking.WabbajackClientApi;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using Wabbajack.Models;

namespace Wabbajack
{
    public class NavigationVM : ViewModel
    {
        private readonly ILogger<NavigationVM> _logger;
        [Reactive]
        public ScreenType ActiveScreen { get; set; }
        public NavigationVM(ILogger<NavigationVM> logger)
        {
            _logger = logger;
            HomeCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.Home));
            BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.ModListGallery));
            InstallCommand = ReactiveCommand.Create(() =>
            {
                LoadLastLoadedModlist.Send();
                NavigateToGlobal.Send(ScreenType.Installer);
            });
            CreateModListCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.CreateModList));
            SettingsCommand = ReactiveCommand.Create(
                /*
                canExecute: this.WhenAny(x => x.ActivePane)
                    .Select(active => !object.ReferenceEquals(active, SettingsPane)),
                */
                execute: () => NavigateToGlobal.Send(ScreenType.Settings));
            MessageBus.Current.Listen<NavigateToGlobal>()
                .Subscribe(x => ActiveScreen = x.Screen)
                .DisposeWith(CompositeDisposable);

            var processLocation = Process.GetCurrentProcess().MainModule?.FileName ?? throw new Exception("Process location is unavailable!");
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyLocation = assembly.Location;
            var fvi = FileVersionInfo.GetVersionInfo(string.IsNullOrWhiteSpace(assemblyLocation) ? processLocation : assemblyLocation);
            Version = $"{fvi.FileVersion}";
        }

        public ICommand HomeCommand { get; }
        public ICommand BrowseCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand CreateModListCommand { get; }
        public ICommand SettingsCommand { get; }
        public string Version { get; }
    }
}
