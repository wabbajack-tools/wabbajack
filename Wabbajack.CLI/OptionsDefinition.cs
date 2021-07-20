using System;
using System.Linq;
using Markdig.Syntax.Inlines;
using Wabbajack.CLI.Verbs;

namespace Wabbajack.CLI
{
    public class OptionsDefinition
    {
        public static Type[] AllOptions =>
            typeof(OptionsDefinition).Assembly.GetTypes().Where(t => t.IsAssignableTo(typeof(AVerb))).ToArray();
    
    }
}
