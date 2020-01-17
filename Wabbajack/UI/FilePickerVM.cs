using DynamicData;
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

        public enum CheckOptions
        {
            Off,
            IfPathNotEmpty,
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
        public CheckOptions ExistCheckOption { get; set; }

        [Reactive]
        public CheckOptions FilterCheckOption { get; set; } = CheckOptions.IfPathNotEmpty;

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

        public SourceList<CommonFileDialogFilter> Filters { get; } = new SourceList<CommonFileDialogFilter>();

        public const string PathDoesNotExistText = "Path does not exist";
        public const string DoesNotPassFiltersText = "Path does not pass designated filters";

        public FilePickerVM(object parentVM = null)
        {
            Parent = parentVM;
            SetTargetPathCommand = ConstructTypicalPickerCommand();

            var existsCheckTuple = Observable.CombineLatest(
                    this.WhenAny(x => x.ExistCheckOption),
                    this.WhenAny(x => x.PathType),
                    this.WhenAny(x => x.TargetPath)
                        // Dont want to debounce the initial value, because we know it's null
                        .Skip(1)
                        .Debounce(TimeSpan.FromMilliseconds(200), RxApp.TaskpoolScheduler)
                        .StartWith(default(string)),
                    resultSelector: (existsOption, type, path) => (ExistsOption: existsOption, Type: type, Path: path))
                .StartWith((ExistsOption: ExistCheckOption, Type: PathType, Path: TargetPath))
                .Replay(1)
                .RefCount();

            var doExistsCheck = existsCheckTuple
                .Select(t =>
                {
                    // Don't do exists type if we don't know what path type we're tracking
                    if (t.Type == PathTypeOptions.Off) return false;
                    switch (t.ExistsOption)
                    {
                        case CheckOptions.Off:
                            return false;
                        case CheckOptions.IfPathNotEmpty:
                            return !string.IsNullOrWhiteSpace(t.Path);
                        case CheckOptions.On:
                            return true;
                        default:
                            throw new NotImplementedException();
                    }
                })
                .Replay(1)
                .RefCount();

            _exists = Observable.Interval(TimeSpan.FromSeconds(3), RxApp.TaskpoolScheduler)
                // Only check exists on timer if desired
                .FlowSwitch(doExistsCheck)
                .Unit()
                // Also check though, when fields change
                .Merge(this.WhenAny(x => x.PathType).Unit())
                .Merge(this.WhenAny(x => x.ExistCheckOption).Unit())
                .Merge(this.WhenAny(x => x.TargetPath).Unit())
                // Signaled to check, get latest params for actual use
                .CombineLatest(existsCheckTuple,
                    resultSelector: (_, tuple) => tuple)
                // Refresh exists
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Select(t =>
                {
                    switch (t.ExistsOption)
                    {
                        case CheckOptions.IfPathNotEmpty:
                            if (string.IsNullOrWhiteSpace(t.Path)) return false;
                            break;
                        case CheckOptions.On:
                            break;
                        case CheckOptions.Off:
                        default:
                            return false;
                    }
                    switch (t.Type)
                    {
                        case PathTypeOptions.Either:
                            return File.Exists(t.Path) || Directory.Exists(t.Path);
                        case PathTypeOptions.File:
                            return File.Exists(t.Path);
                        case PathTypeOptions.Folder:
                            return Directory.Exists(t.Path);
                        case PathTypeOptions.Off:
                        default:
                            return false;
                    }
                })
                .DistinctUntilChanged()
                .StartWith(false)
                .ToGuiProperty(this, nameof(Exists));

            var passesFilters = Observable.CombineLatest(
                    this.WhenAny(x => x.TargetPath),
                    this.WhenAny(x => x.PathType),
                    this.WhenAny(x => x.FilterCheckOption),
                    Filters.Connect().QueryWhenChanged(),
                resultSelector: (target, type, checkOption, query) =>
                {
                    switch (type)
                    {
                        case PathTypeOptions.Either:
                        case PathTypeOptions.File:
                            break;
                        default:
                            return true;
                    }
                    if (query.Count == 0) return true;
                    switch (checkOption)
                    {
                        case CheckOptions.Off:
                            return true;
                        case CheckOptions.IfPathNotEmpty:
                            if (string.IsNullOrWhiteSpace(target)) return true;
                            break;
                        case CheckOptions.On:
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    try
                    {
                        var extension = Path.GetExtension(target);
                        if (extension == null || !extension.StartsWith(".")) return false;
                        extension = extension.Substring(1);
                        if (!query.Any(filter => filter.Extensions.Any(ext => string.Equals(ext, extension)))) return false;
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }
                    return true;
                })
                .StartWith(true)
                .Select(passed =>
                {
                    if (passed) return ErrorResponse.Success;
                    return ErrorResponse.Fail(DoesNotPassFiltersText);
                })
                .Replay(1)
                .RefCount();

            _errorState = Observable.CombineLatest(
                    Observable.CombineLatest(
                            this.WhenAny(x => x.Exists),
                            doExistsCheck,
                            resultSelector: (exists, doExists) => !doExists || exists)
                        .Select(exists => ErrorResponse.Create(successful: exists, exists ? default(string) : PathDoesNotExistText)),
                    passesFilters,
                    this.WhenAny(x => x.AdditionalError)
                        .Select(x => x ?? Observable.Return<IErrorResponse>(ErrorResponse.Success))
                        .Switch(),
                    resultSelector: (existCheck, filter, err) =>
                    {
                        if (existCheck.Failed) return existCheck;
                        if (filter.Failed) return filter;
                        return ErrorResponse.Convert(err);
                    })
                .ToGuiProperty(this, nameof(ErrorState));

            _inError = this.WhenAny(x => x.ErrorState)
                .Select(x => !x.Succeeded)
                .ToGuiProperty(this, nameof(InError));

            // Doesn't derive from ErrorState, as we want to bubble non-empty tooltips,
            // which is slightly different logic
            _errorTooltip = Observable.CombineLatest(
                    Observable.CombineLatest(
                            this.WhenAny(x => x.Exists),
                            doExistsCheck,
                            resultSelector: (exists, doExists) => !doExists || exists)
                        .Select(exists => exists ? default(string) : PathDoesNotExistText),
                    passesFilters
                        .Select(x => x.Reason),
                    this.WhenAny(x => x.AdditionalError)
                        .Select(x => x ?? Observable.Return<IErrorResponse>(ErrorResponse.Success))
                        .Switch(),
                    resultSelector: (exists, filters, err) =>
                    {
                        if (!string.IsNullOrWhiteSpace(exists)) return exists;
                        if (!string.IsNullOrWhiteSpace(filters)) return filters;
                        return err?.Reason;
                    })
                .ToGuiProperty(this, nameof(ErrorTooltip));
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
                    foreach (var filter in Filters.Items)
                    {
                        dlg.Filters.Add(filter);
                    }
                    if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
                    TargetPath = dlg.FileName;
                });
        }
    }
}
