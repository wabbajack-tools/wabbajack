using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
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

        public enum ExistCheckOptions
        {
            Off,
            IfNotEmpty,
            On
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
        public ExistCheckOptions ExistCheckOption { get; set; }

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

            var existsCheckTuple = Observable.CombineLatest(
                    this.WhenAny(x => x.ExistCheckOption),
                    this.WhenAny(x => x.PathType),
                    this.WhenAny(x => x.TargetPath)
                            // Dont want to debounce the initial value, because we know it's null
                            .Skip(1)
                            .Debounce(TimeSpan.FromMilliseconds(200))
                            .StartWith(default(string)),
                    resultSelector: (ExistsOption, Type, Path) => (ExistsOption, Type, Path))
                .Publish()
                .RefCount();

            this._Exists = Observable.Interval(TimeSpan.FromSeconds(3))
                // Only check exists on timer if desired
                .FilterSwitch(existsCheckTuple
                    .Select(t =>
                    {
                        // Don't do exists type if we don't know what path type we're tracking
                        if (t.Type == PathTypeOptions.Off) return false;
                        switch (t.ExistsOption)
                        {
                            case ExistCheckOptions.Off:
                                return false;
                            case ExistCheckOptions.IfNotEmpty:
                                return !string.IsNullOrWhiteSpace(t.Path);
                            case ExistCheckOptions.On:
                                return true;
                            default:
                                throw new NotImplementedException();
                        }
                    }))
                .Unit()
                // Also check though, when fields change
                .Merge(this.WhenAny(x => x.PathType).Unit())
                .Merge(this.WhenAny(x => x.ExistCheckOption).Unit())
                .Merge(this.WhenAny(x => x.TargetPath).Unit())
                // Signaled to check, get latest params for actual use
                .CombineLatest(existsCheckTuple,
                    resultSelector: (_, tuple) => tuple)
                // Refresh exists
                .Select(t =>
                {
                    switch (t.ExistsOption)
                    {
                        case ExistCheckOptions.IfNotEmpty:
                            if (string.IsNullOrWhiteSpace(t.Path)) return true;
                            break;
                        case ExistCheckOptions.On:
                            break;
                        case ExistCheckOptions.Off:
                        default:
                            return true;
                    }
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
                .StartWith(false)
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
