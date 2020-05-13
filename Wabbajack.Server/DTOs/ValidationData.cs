using System.Collections.Generic;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Server.DTOs
{
    public class ValidationData
    {
        public HashSet<(long Game, long ModId, long FileId)> NexusFiles { get; set; }
        public Dictionary<(string PrimaryKeyString, Hash Hash), bool> ArchiveStatus { get; set; }
        public List<(ModlistMetadata Metadata, ModList ModList)> ModLists { get; set; }
    }
}
