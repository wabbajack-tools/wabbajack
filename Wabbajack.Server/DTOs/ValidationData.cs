using System.Collections.Generic;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Server.DTOs
{
    public class ValidationData
    {
        public ConcurrentHashSet<(long Game, long ModId, long FileId)> NexusFiles { get; set; } = new ConcurrentHashSet<(long Game, long ModId, long FileId)>();
        public Dictionary<(string PrimaryKeyString, Hash Hash), bool> ArchiveStatus { get; set; }
        public List<ModlistMetadata> ModLists { get; set; }
        
        public ConcurrentHashSet<(Game Game, long ModId)> SlowQueriedFor { get; set; } = new ConcurrentHashSet<(Game Game, long ModId)>();
    }
}
