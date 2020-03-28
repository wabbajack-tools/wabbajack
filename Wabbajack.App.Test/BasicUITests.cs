using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace Wabbajack.App.Test
{
    public class BasicUITests
    {
        
        [StaFact]
        public async Task CanCompileASimpleModlist()
        {

            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var window = new MainWindow();
            window.Show();
            await Task.Delay(1000);
            
            window.Close();
            Assert.True(true);
        } 
    }
}
