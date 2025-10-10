//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;
using System;
using System.Threading.Tasks;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlService_ExecuteNonQueryAsync_NotConnected_Test
    {
        [TestMethod]
        public async Task ExecuteNonQueryAsync_WithoutDatabase_ThrowsInvalidOperation()
        {
            SqlService svc = new SqlService();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await svc.ExecuteNonQueryAsync("SELECT 1"));
        }
    }
}

