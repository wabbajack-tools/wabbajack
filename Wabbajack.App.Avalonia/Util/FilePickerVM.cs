using DynamicData;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Wabbajack.Abstractions;
using Wabbajack.Extensions;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack
{
    public partial class FilePickerVM : ViewModel
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

        public delegate AbsolutePath TransformPath(AbsolutePath targetPath);
        public TransformPath PathTransformer { get; set; }

        public object Parent { get; }

        // TODO(avalonia-filepicker): FilePickerVM is constructed from many call sites across the
        // codebase (FileUploadVM, CompilerHomeVM, CompilerDetailsVM, ModListGalleryVM, InstallationVM,
        // MO2InstallerVM, etc.). Fully wiring IFilePicker end-to-end would mean updating every one of
        // those constructor calls to supply an instance (typically resolved via DI), which is outside
        // the scope of converting this single file. FilePicker is exposed here as a settable field so
        // callers can assign it (directly or via DI) once they're converted; until it is set, the
        // picker command is a no-op rather than trying to invent a working dialog.
        public IFilePicker FilePicker { get; set; }

        [Reactive]
        public partial ICommand SetTargetPathCommand { get; set; }

        [Reactive]
        public partial AbsolutePath TargetPath { get; set; }

        [Reactive]
        public partial string PromptTitle { get; set; }

        [Reactive]
        public partial PathTypeOptions PathType { get; set; }

        [Reactive]
        public partial CheckOptions ExistCheckOption { get; set; }

        [Reactive]
        public partial CheckOptions FilterCheckOption { get; set; } = CheckOptions.IfPathNotEmpty;

        [Reactive]
        public partial IObservable<IValidationResult> AdditionalError { get; set; }

        private readonly ObservableAsPropertyHelper<bool> _exists;
        public bool Exists => _exists.Value;

        private readonly ObservableAsPropertyHelper<ValidationResult> _validationResult;
        public ValidationResult ValidationResult => _validationResult.Value;

        private readonly ObservableAsPropertyHelper<bool> _inError;
        public bool InError => _inError.Value;

        private readonly ObservableAsPropertyHelper<string> _errorTooltip;
        public string ErrorTooltip => _errorTooltip.Value;

        // TODO(avalonia-filepicker): was SourceList<CommonFileDialogFilter> (Microsoft.WindowsAPICodePack.Dialogs).
        // Filter entries are now plain (Name, Pattern) tuples to match IFilePicker.PickFile's signature.
        // Other view models that still add CommonFileDialogFilter instances to this list (e.g.
        // CompilerHomeVM, CompilerDetailsVM, ModListGalleryVM, InstallationVM) will need to be updated to
        // construct (string Name, string Pattern) tuples instead when they are converted.
        public SourceList<(string Name, string Pattern)> Filters { get; } = new();

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
                        if (!query.Any(filter => filter.Pattern.Split(',').Any(ext => new Extension("." + ext.Trim().TrimStart('*', '.')) == target.Extension))) return false;
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
                    if (passed) return ValidationResult.Success;
                    return ValidationResult.Fail(DoesNotPassFiltersText);
                })
                .Replay(1)
                .RefCount();

            _validationResult = Observable.CombineLatest(
                    Observable.CombineLatest(
                            this.WhenAny(x => x.Exists),
                            doExistsCheck,
                            resultSelector: (exists, doExists) => !doExists || exists)
                        .Select(exists => ValidationResult.Create(successful: exists, exists ? default(string) : PathDoesNotExistText)),
                    passesFilters,
                    this.WhenAny(x => x.AdditionalError)
                        .Select(x => x ?? Observable.Return<IValidationResult>(ValidationResult.Success))
                        .Switch(),
                    resultSelector: (existCheck, filter, err) =>
                    {
                        if (existCheck.Failed) return existCheck;
                        if (filter.Failed) return filter;
                        return ValidationResult.Convert(err);
                    })
                .ToGuiProperty(this, nameof(ValidationResult));

            _inError = this.WhenAny(x => x.ValidationResult)
                .Select(x => x != null && !x.Succeeded)
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
                        .Select(x => x ?? Observable.Return<IValidationResult>(ValidationResult.Success))
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
            return ReactiveCommand.CreateFromTask(
                execute: async () =>
                {
                    if (FilePicker == null)
                    {
                        // TODO(avalonia-filepicker): no IFilePicker has been wired up for this
                        // instance yet (see the FilePicker property above), so there's nothing to do.
                        return;
                    }

                    AbsolutePath? picked = PathType == PathTypeOptions.Folder
                        ? await FilePicker.PickFolder(PromptTitle)
                        : await FilePicker.PickFile(PromptTitle, Filters.Items.ToList());

                    if (picked == null) return;

                    TargetPath = PathTransformer == null ? picked.Value : PathTransformer(picked.Value);

                }, canExecute: canExecute);
        }
    }
}
