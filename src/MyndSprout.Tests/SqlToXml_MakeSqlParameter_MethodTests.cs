//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.Data.SqlClient;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class SqlToXml_MakeSqlParameter_MethodTests
    {
        [TestMethod]
        public void NullName_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => SqlToXml.MakeSqlParameter(null!, "x"));
        }

        [TestMethod]
        public void NullValue_MapsToDBNull()
        {
            var p = SqlToXml.MakeSqlParameter("@p", null);
            Assert.AreEqual("@p", p.ParameterName);
            Assert.AreSame(DBNull.Value, p.Value);
        }
    }
}
