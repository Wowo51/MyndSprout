//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class HardenerOptions_Validate_ThrowsWhenServerMissing
    {
        [TestMethod]
        public void TestMethod()
        {
            var o = new HardenerOptions { Server = "", InternalFirewallOnly = false };
            Assert.ThrowsException<ArgumentException>(() => o.Validate());
        }
    }
}
