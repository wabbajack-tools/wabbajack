using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using AlphaPath = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{
    public class ProcessHelper
    {
        public enum StreamType
        {
            Output, 
            Error,
        }

        public string Path { get; set; } = string.Empty;
        public IEnumerable<object> Arguments { get; set; } = Enumerable.Empty<object>();

        public bool LogError { get; set; } = true;
        
        public readonly Subject<(StreamType Type, string Line)> Output = new Subject<(StreamType Type, string)>(); 
        
        
        public ProcessHelper()
        {
        }

        public async Task<int> Start()
        {
            var info = new ProcessStartInfo
            {
                FileName = (string)Path,
                Arguments = string.Join(" ", Arguments),
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var finished = new TaskCompletionSource<int>();

            var p = new Process
            {
                StartInfo = info,
                EnableRaisingEvents = true
            };
            EventHandler Exited = (sender, args) =>
            {
                finished.SetResult(p.ExitCode);
            };
            p.Exited += Exited;

            DataReceivedEventHandler OutputDataReceived = (sender, data) =>
            {
                if (string.IsNullOrEmpty(data.Data)) return;
                Output.OnNext((StreamType.Output, data.Data));
            };
            p.OutputDataReceived += OutputDataReceived;

            DataReceivedEventHandler ErrorEventHandler = (sender, data) =>
            {
                if (string.IsNullOrEmpty(data.Data)) return;
                Output.OnNext((StreamType.Error, data.Data));
                if (LogError)
                    Utils.Log($"{AlphaPath.GetFileName(Path)} ({p.Id}) StdErr: {data.Data}");
            };
            p.ErrorDataReceived += ErrorEventHandler;

            p.Start();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            ChildProcessTracker.AddProcess(p);

            try
            {
                p.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception)
            {
                // ignored
            }


            var result =  await finished.Task;
            p.CancelErrorRead();
            p.CancelOutputRead();
            p.OutputDataReceived -= OutputDataReceived;
            p.ErrorDataReceived -= ErrorEventHandler;
            p.Exited -= Exited;
            
            Output.OnCompleted();
            return result;
        }
        
    }
}
