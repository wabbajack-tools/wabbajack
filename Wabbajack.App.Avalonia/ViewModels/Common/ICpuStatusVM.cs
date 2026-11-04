using System.Collections.ObjectModel;
using ReactiveUI;

namespace Wabbajack.App.Avalonia.ViewModels.Common;

/// <summary>What CpuView reads: the jobs running now. The installer and compiler view models expose ResourceMonitor.Tasks.</summary>
public interface ICpuStatusVM : IReactiveObject
{
    ReadOnlyObservableCollection<CPUDisplayVM> StatusList { get; }
}
