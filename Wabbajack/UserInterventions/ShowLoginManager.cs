using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.UserInterventions
{
    public class ShowLoginManager : AUserIntervention
    {
        public override string ShortDescription => "User requested to show the login manager";

        public override string ExtendedDescription => "User requested to show the UI for managing all the logins supported by Wabbajack";

        public override void Cancel()
        {
        }
    }
}
