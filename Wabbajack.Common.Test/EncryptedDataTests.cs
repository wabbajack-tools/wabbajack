using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class EncryptedDataTests
    {

        [Fact]
        public async Task CanDetectNewEncryptedData()
        {
            var test_string = Guid.NewGuid().ToString();
            var data = new ConcurrentBag<string>();
            var events = Utils.HaveEncryptedJsonObservable(test_string).Subscribe(e =>
            {
                if (e)
                    data.Add(test_string);
                else
                    data.Clear();
            });
            
            test_string.ToEcryptedJson(test_string);
            await Task.Delay(100);
            
            Assert.Contains(test_string, data);


        } 
        
    }
}
