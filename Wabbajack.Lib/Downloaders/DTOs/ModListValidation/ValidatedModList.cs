using System;
using Wabbajack.Common;

namespace Wabbajack.Lib.Downloaders.DTOs.ModListValidation
{
    public class ValidatedModList
    {
        public string MachineURL { get; set; } = "";

        public string Name { get; set; } = "";
        public Version? Version { get; set; }
        public Hash ModListHash { get; set; } = default;
        public ValidatedArchive[] Archives { get; set; } = Array.Empty<ValidatedArchive>();
        public ListStatus Status { get; set; }
    }
}
