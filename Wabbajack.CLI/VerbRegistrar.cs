using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq.Expressions;
using System.Reflection;
using Wabbajack.CLI.Verbs;

namespace Wabbajack.CLI;

public class VerbRegistrar
{
    public List<VerbDefinition> Definitions { get; }= new();

    public void Register<T>(Func<Command> makeCommand)
    {
        Definitions.Add(new VerbDefinition(makeCommand, typeof(T)));
    }
}

public record VerbDefinition(Func<Command> MakeCommand, Type VerbType)
{
    
}