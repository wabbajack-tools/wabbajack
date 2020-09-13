using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA2;
using Wabbajack.Common;
using Wabbajack.SelfExtractorAutomation.Steps;

namespace Wabbajack.SelfExtractorAutomation
{
    public class ExecutionEngine : IDisposable
    {
        private AbsolutePath _exe;
        private AbsolutePath _outputFolder;
        private IAutomationStep[] _steps;

        public FlaUI.Core.Application Application { get; private set; }
        public AutomationBase Automation { get; private set; }
        
        public Window Window { get; set; }
        

        public ExecutionEngine(AbsolutePath exe, AbsolutePath outputFolder, IAutomationStep[] steps)
        {
            _exe = exe;
            _outputFolder = outputFolder;
            _steps = steps;
        }

        public async Task Run()
        {
            Application = Application.Launch(_exe.ToString());
            ChildProcessTracker.AddProcess(Process.GetProcessById(Application.ProcessId));
            Automation = new UIA2Automation();
            Application.WaitWhileBusy();
           // await Task.Delay(1000);
            SetMainWindow();
            

            foreach (var step in _steps)
            {
                Utils.Log($"Step {step}");
                await Task.Delay(1000);
                await step.Execute(this);

                Application.WaitWhileBusy();
                try
                {
                    if (!Window.IsAvailable)
                    {
                        SetMainWindow();
                    }
                }
                catch (InvalidOperationException)
                {
                    break;
                }
            }
        }

        public void SetMainWindow()
        {
            while (Window == null)
            {
                Window = Application.GetMainWindow(Automation, TimeSpan.FromMilliseconds(250));
            }

            if (!Window.IsAvailable)
            {
                Window = null;
                while (Window == null)
                {

                    Window = Application.GetAllTopLevelWindows(Automation).FirstOrDefault(w => w.IsAvailable);

                    Application.WaitWhileBusy();
                }
            }

            if (Window.IsAvailable)
            {
                Window.AsWindow().SetTransparency(0);
            }
        }

        public string InterpretText(string value)
        {
            if (value == "@OUTPUT_FOLDER@")
                return _outputFolder.ToString();
            return value;
        }

        public void SetProgress(Percent percent)
        {
            Utils.Status($"Extracting {_exe}", percent);
        }

        public void Dispose()
        {
            Application?.Dispose();
            Automation?.Dispose();
        }
    }
}
