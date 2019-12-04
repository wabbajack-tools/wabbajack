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
        public override string ExtendedDescription { get;}

        public GenericInfo(string short_description, string long_description = "")
        {
            ShortDescription = short_description;
            ExtendedDescription = long_description;
        }

        public override string ToString()
        {
            return ShortDescription;
        }
    }
}
