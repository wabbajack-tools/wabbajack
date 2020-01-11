using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Binding;
using ReactiveUI;

namespace Wabbajack
{
    public interface ICpuStatusVM : IReactiveObject
    {
        ObservableCollectionExtended<CPUDisplayVM> StatusList { get; }
        MainWindowVM MWVM { get; }
    }
}
