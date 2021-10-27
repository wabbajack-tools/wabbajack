using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;



[assembly: TestFramework("Wabbajack.App.Test.AvaloniaUiTestFramework", "Wabbajack.App.Test")]
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = false, MaxParallelThreads = 1)]
namespace Wabbajack.App.Test
{
    public class AvaloniaUiTestFramework : XunitTestFramework
    {
        public AvaloniaUiTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {

        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
            => new Executor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

        private class Executor : XunitTestFrameworkExecutor
        {
            public Executor(
                AssemblyName assemblyName,
                ISourceInformationProvider sourceInformationProvider,
                IMessageSink diagnosticMessageSink)
                : base(
                    assemblyName,
                    sourceInformationProvider,
                    diagnosticMessageSink)
            {

            }

            protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases,
                IMessageSink executionMessageSink,
                ITestFrameworkExecutionOptions executionOptions)
            {
                executionOptions.SetValue("xunit.execution.DisableParallelization", false);
                using var assemblyRunner = new Runner(
                    TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink,
                    executionOptions);

                await assemblyRunner.RunAsync();
            }
        }

        private class Runner : XunitTestAssemblyRunner
        {
            public Runner(
                ITestAssembly testAssembly,
                IEnumerable<IXunitTestCase> testCases,
                IMessageSink diagnosticMessageSink,
                IMessageSink executionMessageSink,
                ITestFrameworkExecutionOptions executionOptions)
                : base(
                    testAssembly,
                    testCases,
                    diagnosticMessageSink,
                    executionMessageSink,
                    executionOptions)
            {

            }

            public override void Dispose()
            {
                AvaloniaApp.Stop();

                base.Dispose();
            }

            protected override void SetupSyncContext(int maxParallelThreads)
            {
                var tcs = new TaskCompletionSource<SynchronizationContext>();
                var thread = new Thread(() =>
                {
                    try
                    {
                        AvaloniaApp
                            .BuildAvaloniaApp()
                            .AfterSetup(_ =>
                            {
                                tcs.SetResult(SynchronizationContext.Current);
                            })
                            .StartWithClassicDesktopLifetime(new string[0]);
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                })
                {
                    IsBackground = true
                };
                thread.SetApartmentState(ApartmentState.STA);

                thread.Start();

                SynchronizationContext.SetSynchronizationContext(tcs.Task.Result);
            }
        }
    }
}