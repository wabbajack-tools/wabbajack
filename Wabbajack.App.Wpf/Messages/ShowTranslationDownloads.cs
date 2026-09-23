using System.Threading.Tasks;
using ReactiveUI;

namespace Wabbajack.Messages;

public class ShowTranslationDownloads
{
    private ShowTranslationDownloads(TranslationDownloadsVM viewModel)
    {
        ViewModel = viewModel;
    }

    public TranslationDownloadsVM ViewModel { get; }

    public static Task Send(TranslationDownloadsVM viewModel)
    {
        MessageBus.Current.SendMessage(new ShowTranslationDownloads(viewModel));
        return viewModel.Closed;
    }
}
