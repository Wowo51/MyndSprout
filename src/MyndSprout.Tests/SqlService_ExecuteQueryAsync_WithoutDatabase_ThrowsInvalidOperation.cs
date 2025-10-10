//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace MyndSprout.Tests
{
    [TestClass]
    public class SqlService_ExecuteQueryAsync_WithoutDatabase_ThrowsInvalidOperation
    {
        [TestMethod]
        public async Task ExecuteQueryAsync_WithoutDatabase_ThrowsInvalidOperation()
        {
            var s = new MyndSprout.SqlService();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await s.ExecuteQueryAsync("select 1");
            });
        }
    }
}

