using System;

namespace Wabbajack.Lib.Interventions
{
    public abstract class AErrorMessage : Exception, IException
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public abstract string ShortDescription { get; }
        public abstract string ExtendedDescription { get; }
        Exception IException.Exception => this;
    }
}
