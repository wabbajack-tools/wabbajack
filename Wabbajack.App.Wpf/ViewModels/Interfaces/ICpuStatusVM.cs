using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Binding;
using ReactiveUI;

namespace Wabbajack
{
    public interface ICpuStatusVM : IReactiveObject
    {
        ReadOnlyObservableCollection<CPUDisplayVM> StatusList { get; }
    }
}
