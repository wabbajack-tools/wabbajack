using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.Test
{
    [TestClass]
    public class EncryptedDataTests
    {

        [TestMethod]
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
            
            CollectionAssert.Contains(data, test_string);


        } 
        
    }
}
