using System;
using Wabbajack.Common;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public abstract class ATestBase : XunitContextBase
    {
        private IDisposable _unsub;

        protected ATestBase(ITestOutputHelper output) : base(output)
        {
            _unsub = Utils.LogMessages.Subscribe(f => XunitContext.WriteLine($"{DateTime.Now} - {f}"));
        }

        public override void Dispose()
        {
            _unsub.Dispose();
            base.Dispose();
        }
    }
}
