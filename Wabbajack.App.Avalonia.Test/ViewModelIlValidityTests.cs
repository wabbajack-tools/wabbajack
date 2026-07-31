using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using ReactiveUI;
using Wabbajack;
using Xunit;

// Not Wabbajack.App.*: that namespace would shadow the Wabbajack.App type the headless test host
// has to name (CS0118).
namespace Wabbajack.AvaloniaTests;

/// <summary>
/// Regression test for the ReactiveUI.Fody -> ReactiveUI.SourceGenerators migration.
///
/// ReactiveUI.Fody wove the <c>[Reactive]</c> property-change plumbing into the assembly's IL at
/// build time. On .NET 10 that woven IL was rejected by the JIT with
/// <see cref="InvalidProgramException"/> ("Common Language Runtime detected an invalid program")
/// the moment a view model such as <c>CompilerMainVM</c> was constructed, so the app died on
/// startup inside <c>App.OpenUI()</c> while resolving the main window from the DI container.
///
/// This test forces the JIT to compile every constructor and property accessor of every
/// <see cref="ReactiveObject"/>-derived type in the Wabbajack.App.Avalonia assembly. Invalid IL from a
/// weaver (or any future codegen regression) surfaces as <see cref="InvalidProgramException"/> /
/// <see cref="BadImageFormatException"/> at <see cref="RuntimeHelpers.PrepareMethod(RuntimeMethodHandle)"/>
/// time — reproducing the crash without needing to stand up the full DI graph or a WPF message loop.
/// </summary>
public class ViewModelIlValidityTests
{
    [Fact]
    public void AllReactiveViewModels_HaveValidIl()
    {
        var assembly = typeof(CompilerMainVM).Assembly;

        var reactiveTypes = SafeGetTypes(assembly)
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => typeof(ReactiveObject).IsAssignableFrom(t))
            .ToArray();

        // Guard against a vacuous pass: if reflection can't see the view models (e.g. the assembly
        // failed to load) the test must not silently succeed.
        reactiveTypes.Length.Should().BeGreaterThan(20,
            "the Wabbajack.App.Avalonia assembly should expose many ReactiveObject view models");

        const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.DeclaredOnly;

        var invalidIl = new List<string>();
        var prepared = 0;

        foreach (var type in reactiveTypes)
        {
            var methods = type.GetConstructors(members)
                .Cast<MethodBase>()
                .Concat(type.GetProperties(members)
                    .SelectMany(p => new[] { p.GetMethod, p.SetMethod })
                    .Where(m => m is not null)
                    .Cast<MethodBase>());

            foreach (var method in methods)
            {
                if (method.ContainsGenericParameters)
                    continue;

                try
                {
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    prepared++;
                }
                catch (InvalidProgramException e)
                {
                    invalidIl.Add($"{type.FullName}.{method.Name}: {e.GetType().Name}: {e.Message}");
                }
                catch (BadImageFormatException e)
                {
                    invalidIl.Add($"{type.FullName}.{method.Name}: {e.GetType().Name}: {e.Message}");
                }
                catch (Exception)
                {
                    // Missing dependencies / type-load failures in the test host are unrelated to IL
                    // validity — only invalid IL is a regression of the fix under test.
                }
            }
        }

        prepared.Should().BeGreaterThan(0, "at least some view-model methods should have been JIT-compiled");
        invalidIl.Should().BeEmpty("view-model IL must JIT-compile on .NET 10 (no ReactiveUI.Fody weaving regression)");
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Cast<Type>();
        }
    }
}
