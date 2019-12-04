using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed.Errors
{
    class FileExtractionError : AStatusMessage, IError
    {
        private string _filename;
        private string _destination;
        public override string ShortDescription { get; }
        public override string ExtendedDescription { get; }

        public FileExtractionError(string filename, string destination)
        {
            _filename = filename;
            _destination = destination;
        }
    }
}
