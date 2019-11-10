using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class FilePickerVM : ViewModel
    {
        public enum PathTypeOptions
        {
            Off,
            Either,
            File,
            Folder
        }

        public object Parent { get; }

        [Reactive]
        public ICommand SetTargetPathCommand { get; set; }

        [Reactive]
        public string TargetPath { get; set; }

        [Reactive]
        public string PromptTitle { get; set; }

        [Reactive]
        public PathTypeOptions PathType { get; set; }

        [Reactive]
        public bool DoExistsCheck { get; set; }

        [Reactive]
        public IObservable<IErrorResponse> AdditionalError { get; set; }

        private readonly ObservableAsPropertyHelper<bool> _Exists;
        public bool Exists => _Exists.Value;

        private readonly ObservableAsPropertyHelper<ErrorResponse> _ErrorState;
        public ErrorResponse ErrorState => _ErrorState.Value;

        private readonly ObservableAsPropertyHelper<bool> _InError;
        public bool InError => _InError.Value;

        private readonly ObservableAsPropertyHelper<string> _ErrorTooltip;
        public string ErrorTooltip => _ErrorTooltip.Value;

        public List<CommonFileDialogFilter> Filters { get; } = new List<CommonFileDialogFilter>();

        public FilePickerVM(object parentVM = null)
        {
            this.Parent = parentVM;
            this.SetTargetPathCommand = ConstructTypicalPickerCommand();

            // Check that file exists
            this._Exists = Observable.Interval(TimeSpan.FromSeconds(3))
                .FilterSwitch(
                    Observable.CombineLatest(
                        this.WhenAny(x => x.PathType),
                        this.WhenAny(x => x.DoExistsCheck),
                        resultSelector: (type, doExists) => type != PathTypeOptions.Off && doExists))
                .Unit()
                // Also do it when fields change
                .Merge(this.WhenAny(x => x.PathType).Unit())
                .Merge(this.WhenAny(x => x.DoExistsCheck).Unit())
                .CombineLatest(
                        this.WhenAny(x => x.DoExistsCheck),
                        this.WhenAny(x => x.PathType),
                        this.WhenAny(x => x.TargetPath)
                            .Throttle(TimeSpan.FromMilliseconds(200)),
                    resultSelector: (_, DoExists, Type, Path) => (DoExists, Type, Path))
                // Refresh exists
                .Select(t =>
                {
                    if (!t.DoExists) return true;
                    switch (t.Type)
                    {
                        case PathTypeOptions.Either:
                            return File.Exists(t.Path) || Directory.Exists(t.Path);
                        case PathTypeOptions.File:
                            return File.Exists(t.Path);
                        case PathTypeOptions.Folder:
                            return Directory.Exists(t.Path);
                        default:
                            return true;
                    }
                })
                .DistinctUntilChanged()
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, nameof(this.Exists));

            this._ErrorState = Observable.CombineLatest(
                    this.WhenAny(x => x.Exists)
                        .Select(exists => ErrorResponse.Create(successful: exists, exists ? default(string) : "Path does not exist")),
                    this.WhenAny(x => x.AdditionalError)
                        .Select(x => x ?? Observable.Return<IErrorResponse>(ErrorResponse.Success))
                        .Switch(),
                    resultSelector: (exist, err) =>
                    {
                        if (exist.Failed) return exist;
                        return ErrorResponse.Convert(err);
                    })
                .ToProperty(this, nameof(this.ErrorState));

            this._InError = this.WhenAny(x => x.ErrorState)
                .Select(x => !x.Succeeded)
                .ToProperty(this, nameof(this.InError));

            // Doesn't derive from ErrorState, as we want to bubble non-empty tooltips,
            // which is slightly different logic
            this._ErrorTooltip = Observable.CombineLatest(
                    this.WhenAny(x => x.Exists)
                        .Select(exists => exists ? default(string) : "Path does not exist"),
                    this.WhenAny(x => x.AdditionalError)
                        .Select(x => x ?? Observable.Return<IErrorResponse>(ErrorResponse.Success))
                        .Switch(),
                    resultSelector: (exists, err) =>
                    {
                        if (!string.IsNullOrWhiteSpace(exists)) return exists;
                        return err?.Reason;
                    })
                .ToProperty(this, nameof(this.ErrorTooltip));
        }

        public ICommand ConstructTypicalPickerCommand()
        {
            return ReactiveCommand.Create(
                execute: () =>
                {
                    string dirPath;
                    if (File.Exists(this.TargetPath))
                    {
                        dirPath = System.IO.Path.GetDirectoryName(this.TargetPath);
                    }
                    else
                    {
                        dirPath = this.TargetPath;
                    }
                    var dlg = new CommonOpenFileDialog
                    {
                        Title = this.PromptTitle,
                        IsFolderPicker = this.PathType == PathTypeOptions.Folder,
                        InitialDirectory = this.TargetPath,
                        AddToMostRecentlyUsedList = false,
                        AllowNonFileSystemItems = false,
                        DefaultDirectory = this.TargetPath,
                        EnsureFileExists = true,
                        EnsurePathExists = true,
                        EnsureReadOnly = false,
                        EnsureValidNames = true,
                        Multiselect = false,
                        ShowPlacesList = true,
                    };
                    foreach (var filter in this.Filters)
                    {
                        dlg.Filters.Add(filter);
                    }
                    if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
                    this.TargetPath = dlg.FileName;
                });
        }
    }
}
