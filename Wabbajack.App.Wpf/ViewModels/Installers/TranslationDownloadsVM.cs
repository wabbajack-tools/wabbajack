using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;

namespace Wabbajack;

public class TranslationDownloadsVM : ViewModel, IClosableVM
{
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TranslationDownloadsVM(ManualDownloadsVM downloads, int count, string language)
    {
        Downloads = downloads;
        Title = $"Download {count} {language} translation {(count == 1 ? "file" : "files")}";
        Explanation =
            "Your Nexus Mods account is not premium, so each translation file has to be downloaded in your browser, " +
            "the same way as a modlist's manual downloads. Wabbajack watches your Downloads folder and picks each " +
            "file up as soon as it arrives. Press Continue at any time; anything not downloaded by then is skipped " +
            "and the rest of the translation still goes ahead.";
        CloseCommand = ReactiveCommand.Create(Close);
    }

    public ManualDownloadsVM Downloads { get; }
    public string Title { get; }
    public string Explanation { get; }
    public ICommand CloseCommand { get; }
    public Task Closed => _closed.Task;

    public void Close() => _closed.TrySetResult();
}
