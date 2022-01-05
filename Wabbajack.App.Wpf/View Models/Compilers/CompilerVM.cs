using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Wabbajack.Extensions;
using Wabbajack.Interventions;
using Wabbajack.Messages;
using Wabbajack.RateLimiter;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using DynamicData.Binding;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack
{
    public enum CompilerState
    {
        Configuration,
        Compiling,
        Completed,
        Errored
    }
    public class CompilerVM : BackNavigatingVM
    {
        private readonly DTOSerializer _dtos;

        [Reactive]
        public CompilerState State { get; set; }
        
        [Reactive]
        public ISubCompilerVM SubCompilerVM { get; set; }
        
        // Paths 
        public FilePickerVM ModlistLocation { get; } = new();
        public FilePickerVM DownloadLocation { get; } = new();
        public FilePickerVM OutputLocation { get; } = new();
        
        // Modlist Settings
        
        [Reactive] public string ModListName { get; set; }
        [Reactive] public string Version { get; set; }
        [Reactive] public string Author { get; set; }
        [Reactive] public string Description { get; set; }
        public FilePickerVM ModListImagePath { get; } = new();
        [Reactive] public ImageSource ModListImage { get; set; }
        [Reactive] public string Website { get; set; }
        [Reactive] public string Readme { get; set; }
        [Reactive] public bool IsNSFW { get; set; }
        [Reactive] public bool PublishUpdate { get; set; }
        [Reactive] public string MachineUrl { get; set; }
        [Reactive] public Game BaseGame { get; set; }
        [Reactive] public string SelectedProfile { get; set; }
        [Reactive] public AbsolutePath GamePath { get; set; }
        [Reactive] public bool IsMO2Compilation { get; set; }
        
        [Reactive] public RelativePath[] AlwaysEnabled { get; set; }
        [Reactive] public string[] OtherProfiles { get; set; }
        
        [Reactive] public AbsolutePath Source { get; set; }
        
        public AbsolutePath SettingsOutputLocation => ModlistLocation.TargetPath.Combine(ModListName)
            .WithExtension(IsMO2Compilation ? Ext.MO2CompilerSettings : Ext.CompilerSettings);
        
        public CompilerVM(ILogger<CompilerVM> logger, DTOSerializer dtos) : base(logger)
        {
            _dtos = dtos;
            SubCompilerVM = new MO2CompilerVM(this);
            
            this.WhenActivated(disposables =>
            {
                State = CompilerState.Configuration;
                Disposable.Empty.DisposeWith(disposables);

                ModlistLocation.WhenAnyValue(vm => vm.TargetPath)
                    .Subscribe(p => InferModListFromLocation(p).FireAndForget())
                    .DisposeWith(disposables);
            });
        }

        private async Task InferModListFromLocation(AbsolutePath settingsFile)
        {
            using var ll = LoadingLock.WithLoading();
            if (settingsFile.FileName == "modlist.txt".ToRelativePath() && settingsFile.Depth > 3)
            {
                var mo2Folder = settingsFile.Parent.Parent.Parent;
                var mo2Ini = mo2Folder.Combine(Consts.MO2IniName);
                if (mo2Ini.FileExists())
                {
                    var iniData = mo2Ini.LoadIniFile();

                    var general = iniData["General"];

                    BaseGame = GameRegistry.GetByFuzzyName(general["gameName"].FromMO2Ini()).Game;
                    Source = mo2Folder;

                    SelectedProfile = general["selected_profile"].FromMO2Ini();
                    GamePath = general["gamePath"].FromMO2Ini().ToAbsolutePath();
                    ModListName = SelectedProfile;

                    var settings = iniData["Settings"];
                    DownloadLocation.TargetPath = settings["download_directory"].FromMO2Ini().ToAbsolutePath();
                    IsMO2Compilation = true;


                    
                    AlwaysEnabled = Array.Empty<RelativePath>();
                    // Find Always Enabled mods
                    foreach (var modFolder in mo2Folder.Combine("mods").EnumerateDirectories())
                    {
                        var iniFile = modFolder.Combine("meta.ini");
                        if (!iniFile.FileExists()) continue;

                        var data = iniFile.LoadIniFile();
                        var generalModData = data["General"];
                        if ((generalModData["notes"]?.Contains("WABBAJACK_ALWAYS_ENABLE") ?? false) ||
                            (generalModData["comments"]?.Contains("WABBAJACK_ALWAYS_ENABLE") ?? false))
                            AlwaysEnabled = AlwaysEnabled.Append(modFolder.RelativeTo(mo2Folder)).ToArray();
                    }

                    var otherProfilesFile = settingsFile.Parent.Combine("otherprofiles.txt");
                    if (otherProfilesFile.FileExists())
                    {
                        OtherProfiles = await otherProfilesFile.ReadAllLinesAsync().ToArray();
                    }

                    if (mo2Folder.Depth > 1)
                        OutputLocation.TargetPath = mo2Folder.Parent;

                    await SaveSettingsFile();
                    ModlistLocation.TargetPath = SettingsOutputLocation;
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

                    
        private CompilerSettings GetSettings()
        {
            return new CompilerSettings
            {
                ModListName = ModListName,
                ModListAuthor = Author,
                Downloads = DownloadLocation.TargetPath,
                Source = ModlistLocation.TargetPath,
                Game = BaseGame,
                Profile = SelectedProfile,
                UseGamePaths = true,
                OutputFile = OutputLocation.TargetPath.Combine(SelectedProfile).WithExtension(Ext.Wabbajack),
                AlwaysEnabled = AlwaysEnabled.ToArray(),
                OtherProfiles = OtherProfiles.ToArray()
            };
        }
    }
}
