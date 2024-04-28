using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack
{
    public class CreatedModlistVM
    {
        private ILogger _logger;
        [Reactive]
        public CompilerSettings CompilerSettings { get; set; }

        public CreatedModlistVM(ILogger logger, CompilerSettings compilerSettings)
        {
            _logger = logger;
            CompilerSettings = compilerSettings;
        }
    }
}
