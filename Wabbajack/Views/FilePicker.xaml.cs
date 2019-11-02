using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for FilePicker.xaml
    /// </summary>
    public partial class FilePicker : UserControlRx
    {
        public enum PathTypeOptions
        {
            Off,
            Either,
            File,
            Folder
        }

        public ICommand SetTargetPathCommand
        {
            get => (ICommand)GetValue(SetTargetPathCommandProperty);
            set => SetValue(SetTargetPathCommandProperty, value);
        }
        public static readonly DependencyProperty SetTargetPathCommandProperty = DependencyProperty.Register(nameof(SetTargetPathCommand), typeof(ICommand), typeof(FilePicker),
            new FrameworkPropertyMetadata(default(ICommand)));

        public string TargetPath
        {
            get { return (string)GetValue(TargetPathProperty); }
            set { SetValue(TargetPathProperty, value); }
        }
        public static readonly DependencyProperty TargetPathProperty = DependencyProperty.Register(nameof(TargetPath), typeof(string), typeof(FilePicker),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

        public bool ShowTextBoxInput
        {
            get => (bool)GetValue(ShowTextBoxInputProperty);
            set => SetValue(ShowTextBoxInputProperty, value);
        }
        public static readonly DependencyProperty ShowTextBoxInputProperty = DependencyProperty.Register(nameof(ShowTextBoxInput), typeof(bool), typeof(FilePicker),
             new FrameworkPropertyMetadata(true));

        public PathTypeOptions PathType
        {
            get => (PathTypeOptions)GetValue(PathTypeProperty);
            set => SetValue(PathTypeProperty, value);
        }
        public static readonly DependencyProperty PathTypeProperty = DependencyProperty.Register(nameof(PathType), typeof(PathTypeOptions), typeof(FilePicker),
             new FrameworkPropertyMetadata(PathTypeOptions.Off, WireNotifyPropertyChanged));

        public bool Exists
        {
            get => (bool)GetValue(ExistsProperty);
            set => SetValue(ExistsProperty, value);
        }
        public static readonly DependencyProperty ExistsProperty = DependencyProperty.Register(nameof(Exists), typeof(bool), typeof(FilePicker),
             new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

        public bool DoExistsCheck
        {
            get => (bool)GetValue(DoExistsCheckProperty);
            set => SetValue(DoExistsCheckProperty, value);
        }
        public static readonly DependencyProperty DoExistsCheckProperty = DependencyProperty.Register(nameof(DoExistsCheck), typeof(bool), typeof(FilePicker),
             new FrameworkPropertyMetadata(true, WireNotifyPropertyChanged));

        public string PromptTitle
        {
            get => (string)GetValue(PromptTitleProperty);
            set => SetValue(PromptTitleProperty, value);
        }
        public static readonly DependencyProperty PromptTitleProperty = DependencyProperty.Register(nameof(PromptTitle), typeof(string), typeof(FilePicker),
             new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Filter
        {
            get => (string)GetValue(FilterProperty);
            set => SetValue(FilterProperty, value);
        }
        public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(string), typeof(FilePicker),
             new FrameworkPropertyMetadata(default(string)));

        public FilePicker()
        {
            InitializeComponent();
            this.SetTargetPathCommand = ReactiveCommand.Create(
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
                    var dlg = new CommonOpenFileDialog();
                    dlg.Title = this.PromptTitle;
                    dlg.IsFolderPicker = this.PathType == PathTypeOptions.Folder;
                    dlg.InitialDirectory = this.TargetPath;

                    dlg.AddToMostRecentlyUsedList = false;
                    dlg.AllowNonFileSystemItems = false;
                    dlg.DefaultDirectory = this.TargetPath;
                    dlg.EnsureFileExists = true;
                    dlg.EnsurePathExists = true;
                    dlg.EnsureReadOnly = false;
                    if (!string.IsNullOrWhiteSpace(this.Filter))
                    {
                        var split = this.Filter.Split('|');
                        if (split.Length == 2)
                        {
                            dlg.Filters.Add(new CommonFileDialogFilter(split[0], split[1]));
                        }
                    }
                    dlg.EnsureValidNames = true;
                    dlg.Multiselect = false;
                    dlg.ShowPlacesList = true;
                    if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
                    this.TargetPath = dlg.FileName;
                });

            // Check that file exists
            Observable.Interval(TimeSpan.FromSeconds(3))
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
                .Subscribe(exists => this.Exists = exists)
                .DisposeWith(this.CompositeDisposable);
        }
    }
}
