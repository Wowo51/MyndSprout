//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace MyndSprout.Tests
{
    [TestClass]
    public class AgentFactory_ResolveDefaultServerConnectionString_OverrideTests
    {
        [TestMethod]
        public void ResolveDefaultServerConnectionString_ReturnsOverride_WhenProvided()
        {
            // Arrange
            string overrideConn = "Server=somehost;Database=master;Trusted_Connection=True;";
            var t = typeof(AgentFactory);
            var mi = t.GetMethod("ResolveDefaultServerConnectionString", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "Method ResolveDefaultServerConnectionString not found via reflection.");

            // Act
            var result = mi!.Invoke(null, new object?[] { overrideConn });

            // Assert
            Assert.IsInstanceOfType(result, typeof(string));
            Assert.AreEqual(overrideConn, (string)result!);
        }
    }
}

