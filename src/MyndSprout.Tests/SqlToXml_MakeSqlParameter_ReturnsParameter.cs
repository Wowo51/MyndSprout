//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace MyndSprout.Tests
{
    [TestClass]
    public class SqlToXml_MakeSqlParameter_ReturnsParameter
    {
        [TestMethod]
        public void MakeSqlParameter_GivenNameAndValue_ReturnsSqlParameter()
        {
            var dto = new XmlSerializableSqlParameter
            {
                ParameterName = "p1",
                Value = "val",
                IsNull = false,
                Direction = "Input"
            };

            MethodInfo? mi = typeof(SqlService).GetMethod("MakeSqlParameter", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "Could not find non-public MakeSqlParameter method on SqlService.");

            object? result = mi.Invoke(null, new object[] { dto });
            Assert.IsNotNull(result, "Invocation returned null.");

            SqlParameter? param = result as SqlParameter;
            Assert.IsNotNull(param, "Result is not a SqlParameter.");
            Assert.IsTrue(param.ParameterName.Contains("p1"));
            Assert.AreEqual("val", param.Value?.ToString());
        }
    }
}
