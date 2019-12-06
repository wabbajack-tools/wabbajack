using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed.Errors
{
    public class UnconvertedError : AErrorMessage
    {
        private string _msg;

        public UnconvertedError(string msg)
        {
            _msg = msg;
        }

        public override string ShortDescription { get => _msg; }
        public override string ExtendedDescription { get; } = "";
    }
}
