using ReactiveUI;
using Wabbajack.Abstractions;

namespace Wabbajack.Messages;

public class TaskBarUpdate
{
    public string Description { get; init; }
    public double ProgressValue { get; init; }
    public TaskbarProgressState State { get; init;  }

    public static void Send(string description, TaskbarProgressState state = TaskbarProgressState.None,
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
