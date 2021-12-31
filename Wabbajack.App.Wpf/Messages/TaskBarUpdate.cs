using System.Windows.Shell;
using ReactiveUI;

namespace Wabbajack.Messages;

public class TaskBarUpdate
{
    public string Description { get; init; }
    public double ProgressValue { get; init; }
    public TaskbarItemProgressState State { get; init;  }

    public static void Send(string description, TaskbarItemProgressState state = TaskbarItemProgressState.None,
        double progressValue = 0)
    {
        MessageBus.Current.SendMessage(new TaskBarUpdate()
        {
            Description = description,
            ProgressValue = progressValue,
            State = state
        });
    }
}
