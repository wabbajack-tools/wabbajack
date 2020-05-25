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
            var testString = Guid.NewGuid().ToString();
            var data = new ConcurrentBag<string>();
            var events = Utils.HaveEncryptedJsonObservable(testString).Subscribe(e =>
            {
                if (e)
                    data.Add(testString);
                else
                    data.Clear();
            });
            
            await testString.ToEcryptedJson(testString);
            await Task.Delay(100);
            
            Assert.Contains(testString, data);


        } 
        
    }
}
