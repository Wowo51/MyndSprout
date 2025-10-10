//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyndSprout.Tests
{
    [TestClass]
    public class SqlToXml_MakeSqlParameter_NullName_ThrowsArgumentNullException
    {
        [TestMethod]
        public void MakeSqlParameter_NullName_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => SqlToXml.MakeSqlParameter(null!, 1));
        }
    }
}

