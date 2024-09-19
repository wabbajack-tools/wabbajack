using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CompilerMainView.xaml
/// </summary>
public partial class CompilerMainView : ReactiveUserControl<CompilerMainVM>
{
    public CompilerMainView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            Disposable.Empty.DisposeWith(disposables);
        });

    }
}
