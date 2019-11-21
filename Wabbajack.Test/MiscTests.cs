using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using MahApps.Metro.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils = Wabbajack.Common.Utils;

namespace Wabbajack.Test
{
    [TestClass]
    public class MiscTests
    {
        [TestMethod]
        public void TestDiskSpeed()
        {
            using (var queue = new WorkQueue())
            {
                var speed = Utils.TestDiskSpeed(queue, @".\");
            }
        } 
    }
}
