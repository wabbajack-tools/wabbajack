using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class ProcessHelper
    {
        public enum StreamType
        {
            Output, 
            Error,
        }
        
        public AbsolutePath Path { get; set; }
        public IEnumerable<object> Arguments { get; set; }

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
                Arguments = string.Join(" ", Arguments.Select(a => a switch
                {
                    string s => s,
                    AbsolutePath abs => ("\"" + (string)abs + "\""),
                    RelativePath rel => ("\"" + (string)rel + "\""),
                    _ => a.ToString()
                })),
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
            p.Exited += (sender, args) =>
            {
                finished.SetResult(p.ExitCode);
            };

            p.OutputDataReceived += (sender, data) =>
            {
                if (string.IsNullOrEmpty(data.Data)) return;
                Output.OnNext((StreamType.Output, data.Data));
            };

            p.ErrorDataReceived += (sender, data) =>
            {
                if (string.IsNullOrEmpty(data.Data)) return;
                Output.OnNext((StreamType.Error, data.Data));
                if (LogError)
                    Utils.Log($"{(string)Path.FileName} ({p.Id}) StdErr: {data.Data}");
            };

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
            Output.OnCompleted();
            return result;
        }
        
    }
}
