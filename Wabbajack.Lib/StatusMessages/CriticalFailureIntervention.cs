using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib
{
    /// <summary>
    /// This should probably be replaced with an error, but this is just to get messageboxes out of the .Lib library
    /// </summary>
    public class CriticalFailureIntervention : AUserIntervention
    {
        private TaskCompletionSource<ConfirmationIntervention.Choice> _source = new TaskCompletionSource<ConfirmationIntervention.Choice>();
        public Task<ConfirmationIntervention.Choice> Task => _source.Task;

        public CriticalFailureIntervention(string description, string title)
        {
            ExtendedDescription = description;
            ShortDescription = title;
        }
        public override string ShortDescription { get; }
        public override string ExtendedDescription { get; }
        public override void Cancel()
        {
            Handled = true;
            _source.SetResult(ConfirmationIntervention.Choice.Abort);
        }
    }
}
