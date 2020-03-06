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

        public override string ExtendedDescription => $@"Error extracting data from {(string)Filename} to {(string)Destination} via 7zip, error code {Code}, full error output is in the log";

        public _7zipReturnError(int code, AbsolutePath filename, AbsolutePath destination)
        {
            Code = code;
            Filename = filename;
            Destination = destination;
        }
    }
}
