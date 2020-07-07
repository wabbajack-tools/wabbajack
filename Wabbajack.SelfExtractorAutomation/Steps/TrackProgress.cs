using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.SelfExtractorAutomation.Steps
{
    [JsonName("Track Progress")]
    public class TrackProgress : AAutomationStep
    {
        public override string Description { get; }
        public override async Task Execute(ExecutionEngine engine)
        {
            TOP:
            var progress = engine.Window.FindAllDescendants()
                .Where(d => d.ControlType == ControlType.ProgressBar)
                .Select(d => d.AsProgressBar())
                .FirstOrDefault();

            if (progress == null)
            {
                engine.SetMainWindow();
                goto TOP;
            }

            while (progress.IsAvailable)
            {
                var percent = Percent.FactoryPutInRange(progress.Minimum, progress.Maximum, progress.Value);
                if (percent >= Percent.FactoryPutInRange(99, 100))
                    break;
                engine.SetProgress(percent);
                await Task.Delay(100);
            }
        }
    }
}
