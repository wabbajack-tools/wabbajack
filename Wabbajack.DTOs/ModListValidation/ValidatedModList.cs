using System;
using System.Linq;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.ModListValidation;

public class ValidatedModList
{
    public string MachineURL { get; set; } = "";

    public string Name { get; set; }
    public Version? Version { get; set; }
    public Hash ModListHash { get; set; } = default;
    public ValidatedArchive[] Archives { get; set; } = Array.Empty<ValidatedArchive>();
    public ListStatus Status { get; set; }

    public long Failures => Archives.Count(a => a.Status == ArchiveStatus.InValid);
    public Uri SmallImage { get; set; }
    public Uri LargeImage { get; set; }
}