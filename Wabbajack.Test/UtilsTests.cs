using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;

namespace Wabbajack.Test
{
    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        public void IsInPathTests()
        {
            Assert.IsTrue("c:\\foo\\bar.exe".IsInPath("c:\\foo"));
            Assert.IsFalse("c:\\foo\\bar.exe".IsInPath("c:\\fo"));
            Assert.IsTrue("c:\\Foo\\bar.exe".IsInPath("c:\\foo"));
            Assert.IsTrue("c:\\foo\\bar.exe".IsInPath("c:\\Foo"));
            Assert.IsTrue("c:\\foo\\bar.exe".IsInPath("c:\\fOo"));
            Assert.IsTrue("c:\\foo\\bar.exe".IsInPath("c:\\foo\\"));
            Assert.IsTrue("c:\\foo\\bar\\".IsInPath("c:\\foo\\"));
        }
    }
}
