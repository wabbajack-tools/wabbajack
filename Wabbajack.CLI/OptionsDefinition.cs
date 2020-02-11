using System;
using Wabbajack.CLI.Verbs;

namespace Wabbajack.CLI
{
    public class OptionsDefinition
    {
        public static readonly Type[] AllOptions = {
            typeof(OptionsDefinition), 
            typeof(Encrypt), 
            typeof(Decrypt), 
            typeof(Validate), 
            typeof(DownloadUrl), 
            typeof(UpdateModlists), 
            typeof(UpdateNexusCache)
        };
    }
}
