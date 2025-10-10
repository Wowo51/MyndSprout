//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class DenySkipped_AllowsWhenConfigured
    {
        [TestMethod]
        public void TestMethod()
        {
            var opts = new SqlContain.HardenerOptions { AllowSkippedDeny = true };
            string[] skipped = new[] { " DENY CREATE ASSEMBLY TO public; " };

            // Should not throw when configured to allow skipped denies
            SqlContain.DatabaseHardener.ThrowIfSkippedDenies(skipped, opts);
        }
    }
}
