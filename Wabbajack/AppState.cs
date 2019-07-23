using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using Wabbajack.Common;

namespace Wabbajack
{
    internal class AppState
    {
        public class CPUStatus
        {
            public int Progress { get; internal set; }
            public string Msg { get; internal set; }
            public int ID { get; internal set; }
        }

        public volatile bool Dirty;

        private Dispatcher dispatcher;

        public ObservableCollection<string> Log { get; }
        public ObservableCollection<CPUStatus> Status { get; }
        

        public String Mode { get; set; }

        private List<CPUStatus> InternalStatus { get; }
        public string LogFile { get; private set; }

        public AppState(Dispatcher d, String mode)
        {
            LogFile = Assembly.GetExecutingAssembly().Location + ".log";

            if (LogFile.FileExists())
                File.Delete(LogFile);

            Mode = mode;
            Dirty = false;
            dispatcher = d;
            Log = new ObservableCollection<string>();
            Status = new ObservableCollection<CPUStatus>();
            InternalStatus = new List<CPUStatus>();

            var th = new Thread(() => UpdateLoop());
            th.Priority = ThreadPriority.BelowNormal;
            th.IsBackground = true;
            th.Start();
        }

        private void UpdateLoop()
        {
            while (true)
            {
                if (Dirty)
                {
                    lock (InternalStatus)
                    {
                        var data = InternalStatus.ToArray();
                        dispatcher.Invoke(() =>
                        {
                            for (int idx = 0; idx < data.Length; idx += 1)
                            {
                                if (idx >= Status.Count)
                                    Status.Add(data[idx]);
                                else if (Status[idx] != data[idx])
                                    Status[idx] = data[idx];
                            }
                        });
                        Dirty = false;
                    }
                }
                Thread.Sleep(1000);
            }
        }

        public void LogMsg(string msg)
        {
            dispatcher.Invoke(() => Log.Add(msg));
            lock (dispatcher) {
                File.AppendAllText(LogFile, msg + "\r\n");
            }
        }

        public void SetProgress(int id, string msg, int progress)
        {
            lock (InternalStatus)
            {
                Dirty = true;
                while (id >= InternalStatus.Count)
                {
                    InternalStatus.Add(new CPUStatus());
                }

                InternalStatus[id] = new CPUStatus() { ID = id, Msg = msg, Progress = progress };
            }
        }
    }
}