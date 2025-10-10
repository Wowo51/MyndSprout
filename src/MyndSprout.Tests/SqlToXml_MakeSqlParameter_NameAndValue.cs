//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;
using Microsoft.Data.SqlClient;

namespace MyndSprout.Tests
{
    [TestClass]
    public class SqlToXml_MakeSqlParameter_NameAndValue
    {
        [TestMethod]
        public void MakeSqlParameter_WithValue_SetsNameAndValue()
        {
            var p = SqlToXml.MakeSqlParameter("@x", 123);
            Assert.IsNotNull(p);
            Assert.AreEqual("@x", p.ParameterName);
            Assert.AreEqual(123, p.Value);

            var pNull = SqlToXml.MakeSqlParameter("@y", null);
            Assert.IsNotNull(pNull);
            Assert.AreEqual("@y", pNull.ParameterName);
            Assert.AreEqual(DBNull.Value, pNull.Value);
        }
    }
}

