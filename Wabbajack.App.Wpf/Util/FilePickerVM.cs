using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Wabbajack;
using Wabbajack.Extensions;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

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
        public AbsolutePath TargetPath { get; set; }

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

        public SourceList<CommonFileDialogFilter> Filters { get; } = new();

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
                        .ObserveOnGuiThread()
                        .Debounce(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
                        .StartWith(default(AbsolutePath)),
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
                            return t.Path != default;
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
                            if (t.Path == default) return false;
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
                            return t.Path.FileExists() || t.Path.DirectoryExists();
                        case PathTypeOptions.File:
                            return t.Path.FileExists();
                        case PathTypeOptions.Folder:
                            return t.Path.DirectoryExists();
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
                            if (target == default) return true;
                            break;
                        case CheckOptions.On:
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    try
                    {
                        if (!query.Any(filter => filter.Extensions.Any(ext => new Extension("." + ext) == target.Extension))) return false;
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

        public ICommand ConstructTypicalPickerCommand(IObservable<bool> canExecute = null)
        {
            return ReactiveCommand.Create(
                execute: () =>
                {
                    AbsolutePath dirPath;
                    dirPath = TargetPath.FileExists() ? TargetPath.Parent : TargetPath;
                    var dlg = new CommonOpenFileDialog
                    {
                        Title = PromptTitle,
                        IsFolderPicker = PathType == PathTypeOptions.Folder,
                        InitialDirectory = dirPath.ToString(),
                        AddToMostRecentlyUsedList = false,
                        AllowNonFileSystemItems = false,
                        DefaultDirectory = dirPath.ToString(),
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
                    TargetPath = (AbsolutePath)dlg.FileName;
                }, canExecute: canExecute);
        }
    }
}
