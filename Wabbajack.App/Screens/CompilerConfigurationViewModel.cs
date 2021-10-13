using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Mixins;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Extensions;
using Wabbajack.App.Messages;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Consts = Wabbajack.Compiler.Consts;

namespace Wabbajack.App.Screens;

public class CompilerConfigurationViewModel : ViewModelBase, IReceiverMarker
{
    [Reactive]
    public AbsolutePath SettingsFile { get; set; }
    
    [Reactive]
    public AbsolutePath Downloads { get; set; }
    
    [Reactive]
    public GameMetaData BaseGame { get; set; }
    
    [Reactive]
    public AbsolutePath BasePath { get; set; }
    
    [Reactive]
    public AbsolutePath GamePath { get; set; }
    
    [Reactive]
    public string SelectedProfile { get; set; }
    
    [Reactive]
    public AbsolutePath OutputFolder { get; set; }
    
    [Reactive]
    public IEnumerable<GameMetaData> AllGames { get; set; }
    
    [Reactive]
    public ReactiveCommand<Unit, Unit> StartCompilation { get; set; }

    [Reactive] 
    public IEnumerable<RelativePath> AlwaysEnabled { get; set; } = Array.Empty<RelativePath>();


    public CompilerConfigurationViewModel()
    {
        Activator = new ViewModelActivator();

        AllGames = GameRegistry.Games.Values.ToArray();
        
        StartCompilation = ReactiveCommand.Create(() => BeginCompilation());

        OutputFolder = KnownFolders.EntryPoint;
        
        this.WhenActivated(disposables =>
        {
            var tuples = this.WhenAnyValue(vm => vm.SettingsFile)
                .Where(file => file != default)
                .SelectAsync(disposables, InterpretSettingsFile)
                .Where(t => t != default)
                .ObserveOn(RxApp.MainThreadScheduler);

            tuples.Select(t => t.Downloads)
                .BindTo(this, vm => vm.Downloads)
                .DisposeWith(disposables);

            tuples.Select(t => t.Root)
                .BindTo(this, vm => vm.BasePath)
                .DisposeWith(disposables);

            tuples.Select(t => t.Game)
                .BindTo(this, vm => vm.BaseGame)
                .DisposeWith(disposables);

            tuples.Select(t => t.SelectedProfile)
                .BindTo(this, vm => vm.SelectedProfile)
                .DisposeWith(disposables);

        });
    }

    private void BeginCompilation()
    {
        var settings = new MO2CompilerSettings
        {
            Downloads = Downloads,
            Source = BasePath,
            Game = BaseGame.Game,
            Profile = SelectedProfile,
            UseGamePaths = true,
            OutputFile = OutputFolder.Combine(SelectedProfile).WithExtension(Ext.Wabbajack),
            AlwaysEnabled = AlwaysEnabled.ToArray()
        };
        
        MessageBus.Instance.Send(new StartCompilation(settings));
        MessageBus.Instance.Send(new NavigateTo(typeof(CompilationViewModel)));
    }

    public async ValueTask<(AbsolutePath Root, AbsolutePath Downloads, AbsolutePath Settings, GameMetaData Game, string SelectedProfile)> 
        InterpretSettingsFile(AbsolutePath settingsFile)
    {
        if (settingsFile.FileName == "modlist.txt".ToRelativePath() && settingsFile.Depth > 3)
        {
            var mo2Folder = settingsFile.Parent.Parent.Parent;
            var compilerSettingsFile = settingsFile.Parent.Combine(Consts.CompilerSettings);
            var mo2Ini = mo2Folder.Combine(Consts.MO2IniName);
            if (mo2Ini.FileExists())
            {
                var iniData = mo2Ini.LoadIniFile();

                var general = iniData["General"];

                var game = GameRegistry.GetByFuzzyName(general["gameName"].FromMO2Ini());

                var selectedProfile = general["selected_profile"].FromMO2Ini();
                var gamePath = general["gamePath"].FromMO2Ini().ToAbsolutePath();

                var settings = iniData["Settings"];
                var downloadFolder = settings["download_directory"].FromMO2Ini().ToAbsolutePath();


                return (mo2Folder, downloadFolder, compilerSettingsFile, game, selectedProfile);
            }

        }

        return default;
    }

    public bool AddAlwaysExcluded(AbsolutePath path)
    {
        if (!path.InFolder(BasePath)) return false;
        var relative = path.RelativeTo(BasePath);
        AlwaysEnabled = AlwaysEnabled.Append(relative).Distinct().ToArray();
        return true;
    }

    public void RemoveAlwaysExcluded(RelativePath path)
    {
        AlwaysEnabled = AlwaysEnabled.Where(p => p != path).ToArray();
    }
    
}