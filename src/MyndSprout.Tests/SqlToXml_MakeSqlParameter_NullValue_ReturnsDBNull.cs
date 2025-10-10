//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using MyndSprout;
using System;

namespace MyndSprout.Tests
{
    [TestClass]
    public class SqlToXml_MakeSqlParameter_NullValue_ReturnsDBNull
    {
        [TestMethod]
        public void MakeSqlParameter_NullValue_UsesDBNull()
        {
            var p = SqlToXml.MakeSqlParameter("@name", null);
            Assert.IsNotNull(p);
            Assert.AreEqual("@name", p.ParameterName);
            Assert.AreEqual(DBNull.Value, p.Value);
        }
    }
}

