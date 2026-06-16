using ReactiveUI;

namespace Wabbajack.Messages;

public enum TaskbarItemState { None, Normal, Indeterminate, Error, Paused }

public class TaskBarUpdate
{
    public string Description { get; init; }
    public double ProgressValue { get; init; }
    public TaskbarItemState State { get; init;  }

    public static void Send(string description, TaskbarItemState state = TaskbarItemState.None,
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
