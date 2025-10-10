//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTools;

namespace SqlTools.Tests
{
    [TestClass]
    public class FileRecordTests
    {
        [TestMethod]
        public void Defaults_AreEmpty()
        {
            var r = new FileImporter.FileRecord();
            Assert.AreEqual(string.Empty, r.Name);
            Assert.AreEqual(string.Empty, r.Content);
        }
    }
}
