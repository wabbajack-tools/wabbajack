using System;
using System.Collections.Generic;
using Wabbajack.Common.FileSignatures;

namespace Wabbajack.VirtualFileSystem.SevenZipExtractor
{
    public class Formats
    {
      
        internal static Dictionary<Definitions.FileType, Guid> FileTypeGuidMapping = new Dictionary<Definitions.FileType, Guid>
        {
            {Definitions.FileType._7Z, new Guid("23170f69-40c1-278a-1000-000110070000")},
            {Definitions.FileType.BZ2, new Guid("23170f69-40c1-278a-1000-000110020000")},
            {Definitions.FileType.RAR_OLD, new Guid("23170f69-40c1-278a-1000-000110030000")},
            {Definitions.FileType.RAR_NEW, new Guid("23170f69-40c1-278a-1000-000110CC0000")},
            {Definitions.FileType.ZIP, new Guid("23170f69-40c1-278a-1000-000110010000")},
        };

    }
}
