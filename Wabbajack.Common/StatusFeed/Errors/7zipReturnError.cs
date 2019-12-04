using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed.Errors
{
    public class _7zipReturnError : AStatusMessage, IError
    {
        private string _destination;
        private string _filename;
        private int _code;
        private string _7zip_output;
        public override string ShortDescription => $"7Zip returned an error while executing";

        public override string ExtendedDescription =>
            $@"7Zip.exe should always return 0 when it finishes executing. While extracting {_filename} 7Zip encountered some error and
instead returned {_code} which indicates there was an error. The archive might be corrupt or in a format that 7Zip cannot handle. Please verify the file is valid and that you
haven't run out of disk space in the {_destination} folder.

7Zip Output:
{_7zip_output}";

        public _7zipReturnError(int code, string filename, string destination, string output)
        {
            _code = code;
            _filename = filename;
            _destination = destination;
            _7zip_output = output;
        }
    }
}
