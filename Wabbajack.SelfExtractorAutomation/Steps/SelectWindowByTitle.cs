using System.Threading.Tasks;
using FlaUI.Core;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.SelfExtractorAutomation.Steps
{
    [JsonName("SelectWindowByTitle")]
    public class SelectWindowByTitle : AAutomationStep
    {
        public string Title { get; set; }
        public override string Description => $"Select a processes window with the title of '{Title}'";
        public override async Task Execute(ExecutionEngine engine)
        {
            
            

        }
    }
}
