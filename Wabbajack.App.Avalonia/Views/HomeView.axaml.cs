using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

public partial class HomeView : ReactiveUserControl<HomeVM>
{
    public HomeView()
    {
        AvaloniaXamlLoader.Load(this);

        var modlistAmount = this.FindControl<TextBlock>("ModlistAmountTextBlock")!;
        var gameAmount = this.FindControl<TextBlock>("GameAmountTextBlock")!;

        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel!.Modlists)
                .Select(x => x?.Length.ToString() ?? "0")
                .BindTo(modlistAmount, x => x.Text)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel!.Modlists)
                .Select(x => x?.GroupBy(y => y.Game).Count().ToString() ?? "0")
                .BindTo(gameAmount, x => x.Text)
                .DisposeWith(dispose);
        });
    }
}
