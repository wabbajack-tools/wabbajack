using System;
using System.Threading.Tasks;
using FlaUI.Core;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace Wabbajack.SelfExtractorAutomation.Steps
{
    public interface IAutomationStep
    {
        public TimeSpan Timeout { get; }
        
        [JsonIgnore]
        public string Description { get; }

        public Task Execute(ExecutionEngine engine);
    }
}
