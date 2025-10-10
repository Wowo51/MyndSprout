//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.Data.SqlClient;
using SqlTools;

namespace SqlTools.Tests
{
    [TestClass]
    public class ExportSchemaNullArgTests
    {
        [TestMethod]
        public void Export_NullConnection_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ExportSchema.Export(null!));
        }
    }
}
