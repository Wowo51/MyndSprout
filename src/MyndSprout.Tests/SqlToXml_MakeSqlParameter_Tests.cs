//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.Data.SqlClient;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class SqlToXml_MakeSqlParameter_Tests
    {
        [TestMethod]
        public void NullName_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => SqlToXml.MakeSqlParameter(null!, "x"));
        }

        [TestMethod]
        public void NullValue_BecomesDBNull()
        {
            var p = SqlToXml.MakeSqlParameter("p", null);
            Assert.AreEqual("p", p.ParameterName);
            Assert.AreEqual(DBNull.Value, p.Value);
        }

        [TestMethod]
        public void NonNullValue_Preserved()
        {
            var p = SqlToXml.MakeSqlParameter("n", 123);
            Assert.AreEqual("n", p.ParameterName);
            Assert.AreEqual(123, p.Value);
        }
    }
}

