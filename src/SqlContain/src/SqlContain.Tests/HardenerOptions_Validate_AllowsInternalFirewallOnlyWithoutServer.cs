//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class HardenerOptions_Validate_AllowsInternalFirewallOnlyWithoutServer
    {
        [TestMethod]
        public void TestMethod()
        {
            var o = new SqlContain.HardenerOptions
            {
                Server = "",
                InternalFirewallOnly = true,
                Scope = SqlContain.Scope.Database,
                Database = "master"
            };
            // Should not throw
            o.Validate();
        }
    }
}
