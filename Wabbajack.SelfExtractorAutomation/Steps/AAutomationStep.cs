using System;
using System.Threading.Tasks;

namespace Wabbajack.SelfExtractorAutomation.Steps
{
    public abstract class AAutomationStep : IAutomationStep
    {
        public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(10);
        public abstract string Description { get; }
        public abstract Task Execute(ExecutionEngine engine);
    }
}
