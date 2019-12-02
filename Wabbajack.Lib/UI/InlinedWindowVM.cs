using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using ReactiveUI.Fody.Helpers;

namespace Wabbajack.Lib
{
    public class InlinedWindowVM : ViewModel
    {
        [Reactive]
        public ContentControl Content { get; protected set; }

    }
}
