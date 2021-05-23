using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed.Errors
{
    public class _7zipReturnError : AErrorMessage
    {
        public AbsolutePath Destination { get; }
        public AbsolutePath Filename { get; }
        public int Code;
        public override string ShortDescription => $"7Zip returned an error while executing";

        public override string ExtendedDescription =>
            $@"7Zip.exe should always return 0 when it finishes executing. While extracting {(string)Filename} 7Zip encountered some error and
            instead returned {Code} which indicates there was an error. The archive might be corrupt or in a format that 7Zip cannot handle. Please verify the file is valid and that you
            haven't run out of disk space on your {Destination.DriveInfo().Name} drive.";

        public _7zipReturnError(int code, AbsolutePath filename, AbsolutePath destination)
        {
            Code = code;
            Filename = filename;
            Destination = destination;
        }
    }
}
