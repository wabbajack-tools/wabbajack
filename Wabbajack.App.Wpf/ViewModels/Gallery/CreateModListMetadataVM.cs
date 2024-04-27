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
    public class CreateModListMetadataVM : BaseModListMetadataVM
    {
        private CreateModListVM _parent;
        [Reactive]
        public CompilerSettings CompilerSettings { get; set; }

        public CreateModListMetadataVM(ILogger logger, CreateModListVM parent, ModlistMetadata metadata,
            ModListDownloadMaintainer maintainer, Client wjClient, CancellationToken cancellationToken) : base(logger, metadata, maintainer, wjClient, cancellationToken)
        {
            _parent = parent;
            CompilerSettings = _parent.ModLists.FirstOrDefault(ml => ml.Metadata.Links.MachineURL == wjClient.GetMyModlists());
        }
    }
}
