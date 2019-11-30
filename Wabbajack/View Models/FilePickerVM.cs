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

        private readonly ObservableAsPropertyHelper<bool> _exists;
        public bool Exists => _exists.Value;

        private readonly ObservableAsPropertyHelper<ErrorResponse> _errorState;
        public ErrorResponse ErrorState => _errorState.Value;

        private readonly ObservableAsPropertyHelper<bool> _inError;
        public bool InError => _inError.Value;

        private readonly ObservableAsPropertyHelper<string> _errorTooltip;
        public string ErrorTooltip => _errorTooltip.Value;

        public List<CommonFileDialogFilter> Filters { get; } = new List<CommonFileDialogFilter>();

        public FilePickerVM(object parentVM = null)
        {
            Parent = parentVM;
            SetTargetPathCommand = ConstructTypicalPickerCommand();

            // Check that file exists

            var existsCheckTuple = Observable.CombineLatest(
                    this.WhenAny(x => x.ExistCheckOption),
                    this.WhenAny(x => x.PathType),
                    this.WhenAny(x => x.TargetPath)
                            // Dont want to debounce the initial value, because we know it's null
                            .Skip(1)
                            .Debounce(TimeSpan.FromMilliseconds(200))
                            .StartWith(default(string)),
                    resultSelector: (existsOption, type, path) => (ExistsOption: existsOption, Type: type, Path: path))
                .Publish()
                .RefCount();

            _exists = Observable.Interval(TimeSpan.FromSeconds(3))
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
                .ToProperty(this, nameof(Exists));

            _errorState = Observable.CombineLatest(
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
                .ToProperty(this, nameof(ErrorState));

            _inError = this.WhenAny(x => x.ErrorState)
                .Select(x => !x.Succeeded)
                .ToProperty(this, nameof(InError));

            // Doesn't derive from ErrorState, as we want to bubble non-empty tooltips,
            // which is slightly different logic
            _errorTooltip = Observable.CombineLatest(
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
                .ToProperty(this, nameof(ErrorTooltip));
        }

        public ICommand ConstructTypicalPickerCommand()
        {
            return ReactiveCommand.Create(
                execute: () =>
                {
                    string dirPath;
                    if (File.Exists(TargetPath))
                    {
                        dirPath = Path.GetDirectoryName(TargetPath);
                    }
                    else
                    {
                        dirPath = TargetPath;
                    }
                    var dlg = new CommonOpenFileDialog
                    {
                        Title = PromptTitle,
                        IsFolderPicker = PathType == PathTypeOptions.Folder,
                        InitialDirectory = dirPath,
                        AddToMostRecentlyUsedList = false,
                        AllowNonFileSystemItems = false,
                        DefaultDirectory = dirPath,
                        EnsureFileExists = true,
                        EnsurePathExists = true,
                        EnsureReadOnly = false,
                        EnsureValidNames = true,
                        Multiselect = false,
                        ShowPlacesList = true,
                    };
                    foreach (var filter in Filters)
                    {
                        dlg.Filters.Add(filter);
                    }
                    if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
                    TargetPath = dlg.FileName;
                });
        }
    }
}
