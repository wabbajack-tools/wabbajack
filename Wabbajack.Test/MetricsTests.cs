using System;
using System.Threading.Tasks;
using Wabbajack.Lib;
using Xunit;

namespace Wabbajack.Test
{
    public class MetricsTests
    {
        [Fact]
        public async Task CanSendExceptions()
        {
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
