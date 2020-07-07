using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.SelfExtractorAutomation.Steps
{
    [JsonName("SetEditContents")]
    public class SetEditContents : AAutomationStep
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public override string Description { get; }
        public override async Task Execute(ExecutionEngine engine)
        {
            TOP:
            var control = engine.Window.FindAllDescendants()
                .Where(c => c.ControlType == ControlType.Edit && c.Name == Name)
                .Select(tb => tb.AsTextBox())
                .FirstOrDefault();
            if (control == null)
            {
                engine.SetMainWindow();
                await Task.Delay(250);
                goto TOP;
            }
            control.Text = engine.InterpretText(Value);

        }
    }
}
