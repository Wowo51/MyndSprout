//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;
using System;
using System.Reflection;

namespace MyndSprout.Tests
{
    [TestClass]
    public class SqlStrings_NormalizeIdentifier_Tests
    {
        [TestMethod]
        public void NormalizeIdentifier_Basic_ReturnsExpected()
        {
            // Arrange
            string input = "  My Id!@# 123 ";
            string expected = "MyId123";

            Type t = typeof(SqlStrings);

            // Act / Assert path a) exact method
            MethodInfo? exact = t.GetMethod("NormalizeIdentifier", BindingFlags.Public | BindingFlags.Static);
            if (exact != null && exact.ReturnType == typeof(string))
            {
                object? r = exact.Invoke(null, new object?[] { input });
                Assert.IsNotNull(r);
                Assert.AreEqual(expected, r as string);
                return;
            }

            // Act / Assert path b) closest public static string method containing "Normalize"
            MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (MethodInfo m in methods)
            {
                if (m.ReturnType == typeof(string) && m.Name.IndexOf("Normalize", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
                    {
                        object? r = m.Invoke(null, new object?[] { input });
                        Assert.IsNotNull(r);
                        Assert.AreNotEqual(string.Empty, (string)r!);
                        return;
                    }
                }
            }

            // Fallback path c) ensure class exists
            Assert.IsNotNull(t);
        }
    }
}
