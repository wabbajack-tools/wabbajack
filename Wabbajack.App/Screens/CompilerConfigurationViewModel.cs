using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.Mixins;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Consts = Wabbajack.Compiler.Consts;

namespace Wabbajack.App.Screens;

public class CompilerConfigurationViewModel : ViewModelBase
{
    private readonly DTOSerializer _dtos;
    private readonly SettingsManager _settingsManager;


    public CompilerConfigurationViewModel(DTOSerializer dtos, SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        _dtos = dtos;
        Activator = new ViewModelActivator();

        AllGames = GameRegistry.Games.Values.ToArray();

        StartCompilation = ReactiveCommand.Create(() => BeginCompilation().FireAndForget());

        OutputFolder = KnownFolders.EntryPoint;

        this.WhenActivated(disposables =>
        {
            LoadLastCompilation().FireAndForget();
            this.WhenAnyValue(v => v.SettingsFile)
                .Subscribe(location => { LoadNewSettingsFile(location).FireAndForget(); })
                .DisposeWith(disposables);
        });
    }

    [Reactive] public string Title { get; set; }

    [Reactive] public AbsolutePath SettingsFile { get; set; }

    [Reactive] public AbsolutePath Downloads { get; set; }

    [Reactive] public GameMetaData BaseGame { get; set; }

    [Reactive] public AbsolutePath Source { get; set; }

    [Reactive] public AbsolutePath GamePath { get; set; }

    [Reactive] public string SelectedProfile { get; set; }

    [Reactive] public AbsolutePath OutputFolder { get; set; }

    [Reactive] public IEnumerable<GameMetaData> AllGames { get; set; }

    [Reactive] public ReactiveCommand<Unit, Unit> StartCompilation { get; set; }

    [Reactive] public IEnumerable<RelativePath> AlwaysEnabled { get; set; } = Array.Empty<RelativePath>();

    public AbsolutePath SettingsOutputLocation => Source.Combine(Title)
        .WithExtension(IsMO2Compilation ? Ext.MO2CompilerSettings : Ext.CompilerSettings);

    [Reactive] public bool IsMO2Compilation { get; set; }

    private async Task LoadNewSettingsFile(AbsolutePath location)
    {
        if (location == default) return;
        if (location.FileExists()) await LoadSettings(location);
    }

    private async Task LoadLastCompilation()
    {
        var location = await _settingsManager.Load<AbsolutePath>("last_compilation");
        SettingsFile = location;
    }

    private async Task BeginCompilation()
    {
        var settings = GetSettings();
        await SaveSettingsFile();
        await _settingsManager.Save("last_compilation", SettingsOutputLocation);

        MessageBus.Current.SendMessage(new StartCompilation(settings));
        MessageBus.Current.SendMessage(new NavigateTo(typeof(CompilationViewModel)));
    }

    private CompilerSettings GetSettings()
    {
        return new MO2CompilerSettings
        {
            ModListName = Title,
            Downloads = Downloads,
            Source = Source,
            Game = BaseGame.Game,
            Profile = SelectedProfile,
            UseGamePaths = true,
            OutputFile = OutputFolder.Combine(SelectedProfile).WithExtension(Ext.Wabbajack),
            AlwaysEnabled = AlwaysEnabled.ToArray()
        };
    }

    public bool AddAlwaysExcluded(AbsolutePath path)
    {
        if (!path.InFolder(Source)) return false;
        var relative = path.RelativeTo(Source);
        AlwaysEnabled = AlwaysEnabled.Append(relative).Distinct().ToArray();
        return true;
    }

    public void RemoveAlwaysExcluded(RelativePath path)
    {
        AlwaysEnabled = AlwaysEnabled.Where(p => p != path).ToArray();
    }

    public async Task InferSettingsFromModlistTxt(AbsolutePath settingsFile)
    {
        if (settingsFile.FileName == "modlist.txt".ToRelativePath() && settingsFile.Depth > 3)
        {
            var mo2Folder = settingsFile.Parent.Parent.Parent;
            var mo2Ini = mo2Folder.Combine(Consts.MO2IniName);
            if (mo2Ini.FileExists())
            {
                var iniData = mo2Ini.LoadIniFile();

                var general = iniData["General"];

                BaseGame = GameRegistry.GetByFuzzyName(general["gameName"].FromMO2Ini());
                Source = mo2Folder;

                SelectedProfile = general["selected_profile"].FromMO2Ini();
                GamePath = general["gamePath"].FromMO2Ini().ToAbsolutePath();
                Title = SelectedProfile;

                var settings = iniData["Settings"];
                Downloads = settings["download_directory"].FromMO2Ini().ToAbsolutePath();
                IsMO2Compilation = true;


                // Find Always Enabled mods
                foreach (var modFolder in mo2Folder.Combine("mods").EnumerateDirectories())
                {
                    var iniFile = modFolder.Combine("meta.ini");
                    if (!iniFile.FileExists()) continue;

                    var data = iniFile.LoadIniFile();
                    var generalModData = data["General"];
                    AlwaysEnabled = Array.Empty<RelativePath>();
                    if ((generalModData["notes"]?.Contains("WABBAJACK_ALWAYS_ENABLE") ?? false) ||
                        (generalModData["comments"]?.Contains("WABBAJACK_ALWAYS_ENABLE") ?? false))
                        AlwaysEnabled = AlwaysEnabled.Append(modFolder.RelativeTo(mo2Folder)).ToArray();
                }

                if (mo2Folder.Depth > 1)
                    OutputFolder = mo2Folder.Parent;

                await SaveSettingsFile();
                SettingsFile = SettingsOutputLocation;
            }
        }
    }

    private async Task SaveSettingsFile()
    {
        await using var st = SettingsOutputLocation.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        if (IsMO2Compilation)
            await JsonSerializer.SerializeAsync(st, (MO2CompilerSettings) GetSettings(), _dtos.Options);
        else
            await JsonSerializer.SerializeAsync(st, GetSettings(), _dtos.Options);
    }

    private async Task LoadSettings(AbsolutePath path)
    {
        CompilerSettings s;
        if (path.Extension == Ext.MO2CompilerSettings)
        {
            var mo2 = await LoadSettingsFile<MO2CompilerSettings>(path);
            AlwaysEnabled = mo2.AlwaysEnabled;
            SelectedProfile = mo2.Profile;
            s = mo2;
        }
        else
        {
            throw new NotImplementedException();
        }

        Title = s.ModListName;
        Source = s.Source;
        Downloads = s.Downloads;
        OutputFolder = s.OutputFile.Depth > 1 ? s.OutputFile.Parent : s.OutputFile;
        BaseGame = s.Game.MetaData();
    }

    private async Task<T> LoadSettingsFile<T>(AbsolutePath path)
    {
        await using var st = path.Open(FileMode.Open);
        return (await JsonSerializer.DeserializeAsync<T>(st, _dtos.Options))!;
    }
}