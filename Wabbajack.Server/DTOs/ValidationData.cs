using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Server.DTOs
{
    public class ValidationData
    {
        public Dictionary<(long Game, long ModId, long FileId), string> NexusFiles { get; set; } = new ();
        public Dictionary<(string PrimaryKeyString, Hash Hash), bool> ArchiveStatus { get; set; }
        public List<ModlistMetadata> ModLists { get; set; }
        
        public ConcurrentHashSet<(Game Game, long ModId)> SlowQueriedFor { get; set; } = new ConcurrentHashSet<(Game Game, long ModId)>();
        public Dictionary<Hash, bool> Mirrors { get; set; }
        public Lazy<Task<Dictionary<Hash, string>>> AllowedMirrors { get; set; }
        public IEnumerable<AuthoredFilesSummary> AllAuthoredFiles { get; set; }
    }
}
