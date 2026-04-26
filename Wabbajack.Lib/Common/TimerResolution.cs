using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Wabbajack.Common
{
    public sealed class TimerResolution : IDisposable
    {
        [SuppressUnmanagedCodeSecurity]
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        private static extern uint TimeBeginPeriod(uint ms);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        private static extern uint TimeEndPeriod(uint ms);

        private static readonly object Gate = new();
        private static int _refCount;
        private readonly uint _ms;
        private bool _active;

        public TimerResolution(uint milliseconds = 1)
        {
            _ms = milliseconds == 0 ? 1u : milliseconds;
            lock (Gate)
            {
                if (_refCount++ == 0) TimeBeginPeriod(_ms);
                _active = true;
            }
        }

        public void Dispose()
        {
            lock (Gate)
            {
                if (_active && --_refCount == 0) TimeEndPeriod(_ms);
                _active = false;
            }
        }
    }
}