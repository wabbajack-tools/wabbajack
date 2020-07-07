using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.SelfExtractorAutomation.Steps
{
    [JsonName("ClickButton")]
    public class ClickButton : AAutomationStep
    {
        public string Name { get; set; }
        public override string Description { get; }
        public override async Task Execute(ExecutionEngine engine)
        {
            TOP:
            var button = engine.Window.FindAllChildren().Where(e => e.ControlType == ControlType.Button && e.Name == Name)
                .Select(f => f.AsButton())
                .FirstOrDefault();
            if (button == null)
            {
                engine.SetMainWindow();
                await Task.Delay(250);
                goto TOP;
            }

            button.Invoke();
        }
    }
}
