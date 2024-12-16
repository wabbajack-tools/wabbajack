using System.Collections.ObjectModel;
using ReactiveUI;

namespace Wabbajack;

public interface ICpuStatusVM : IReactiveObject
{
    ReadOnlyObservableCollection<CPUDisplayVM> StatusList { get; }
}
