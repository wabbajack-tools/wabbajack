using System;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Xunit;

namespace Wabbajack.Test
{
    public class MetricsTests
    {
        [Fact]
        public async Task CanSendExceptions()
        {
            foreach (var mode in new[] {true, false})
            {
                Consts.UseNetworkWorkaroundMode = mode;
                try
                {
                    throw new Exception("Test Exception");
                }
                catch (Exception ex)
                {
                    await Metrics.Error(this.GetType(), ex);
                }
            }
        }
        
    }
}
