using System;
using Wabbajack.CLI.Verbs;

namespace Wabbajack.CLI
{
    public class OptionsDefinition
    {
        public static Type[] AllOptions = new[]
        {
            typeof(OptionsDefinition), typeof(Encrypt), typeof(Decrypt)
        };
    }
}
