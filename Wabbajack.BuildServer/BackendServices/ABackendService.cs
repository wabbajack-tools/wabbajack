using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.BackendServices
{
    public abstract class ABackendService
    {
        protected ABackendService(SqlService sql, AppSettings settings, TimeSpan pollRate)
        {
            Sql = sql;
            Settings = settings;
            PollRate = pollRate;
        }

        public TimeSpan PollRate { get; }

        public async Task RunLoop(CancellationToken token)
        {
            Utils.Log($"Starting loop for {GetType()}");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Execute();
                }
                catch (Exception ex)
                {
                    Utils.Log($"Error executing {GetType()}");
                    Utils.Log(ex.ToString());
                }

                await Task.Delay(PollRate);
            }
            
        }

        public abstract Task Execute();

        protected AppSettings Settings { get; set; }

        protected SqlService Sql { get; set; }
    }
}
