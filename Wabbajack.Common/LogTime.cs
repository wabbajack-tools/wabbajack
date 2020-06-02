using System;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class LogTime : IAsyncDisposable
    {
        private readonly string _message;
        private readonly DateTime _start;

        public LogTime(string message)
        {
            _message = message;
            _start = DateTime.UtcNow;
        }
        
        public async ValueTask DisposeAsync()
        {
            Utils.Log($"Log Time: {_message} {DateTime.UtcNow - _start}");
        }
    }
}
