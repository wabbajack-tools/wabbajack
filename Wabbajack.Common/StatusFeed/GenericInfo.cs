using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.StatusFeed
{
    public class GenericInfo : AStatusMessage, IInfo
    {
        public override string ShortDescription { get; }
        private readonly string _extendedDescription;
        public override string ExtendedDescription => _extendedDescription ?? ShortDescription;

        public GenericInfo(string short_description, string long_description = null)
        {
            ShortDescription = short_description;
            _extendedDescription = long_description;
        }

        public override string ToString()
        {
            return ShortDescription;
        }
    }
}
