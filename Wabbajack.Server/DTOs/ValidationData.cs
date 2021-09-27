using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.Server.DTOs
{
    public class ValidationData
    {
        public Dictionary<(long Game, long ModId, long FileId), string> NexusFiles { get; set; } = new ();
        public Dictionary<(string PrimaryKeyString, Hash Hash), bool> ArchiveStatus { get; set; }
        public List<ModlistMetadata> ModLists { get; set; }
        public Dictionary<Hash, bool> Mirrors { get; set; }
        public Lazy<Task<Dictionary<Hash, string>>> AllowedMirrors { get; set; }
        public IEnumerable<AuthoredFilesSummary> AllAuthoredFiles { get; set; }
    }
}
