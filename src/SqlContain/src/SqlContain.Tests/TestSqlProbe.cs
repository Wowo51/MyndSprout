//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    internal static class TestSqlProbe
    {
        public static bool TryGetAvailableMasterConnection(out string connectionString, out string message)
        {
            string? envConn = Environment.GetEnvironmentVariable("TEST_SQL_MASTER_CONN");
            if (!string.IsNullOrEmpty(envConn))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(envConn))
                    {
                        conn.Open();
                        conn.Close();
                    }

                    connectionString = envConn;
                    message = "Connected using TEST_SQL_MASTER_CONN";
                    return true;
                }
                catch (Exception ex)
                {
                    // Record diagnostic and fall back to LocalDB probe
                    message = "TEST_SQL_MASTER_CONN provided but connection failed: " + ex.Message;
                }
            }

            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            csb.DataSource = "(LocalDB)\\MSSQLLocalDB";
            csb.IntegratedSecurity = true;
            csb.ConnectTimeout = 15;
            csb.InitialCatalog = "master";

            string candidate = csb.ConnectionString;

            int attempts = 3;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(candidate))
                    {
                        conn.Open();
                        conn.Close();
                    }

                    connectionString = candidate;
                    message = "Connected using LocalDB MSSQLLocalDB";
                    return true;
                }
                catch (Exception ex)
                {
                    if (i == attempts - 1)
                    {
                        Assert.Inconclusive("LocalDB unavailable - test requires LocalDB or TEST_SQL_MASTER_CONN. Details: " + ex.Message);
                        connectionString = string.Empty;
                        message = "LocalDB unavailable: " + ex.Message;
                        return false;
                    }

                    try { System.Threading.Thread.Sleep(500); } catch { /* best-effort */ }
                }
            }

            connectionString = string.Empty;
            message = "LocalDB unavailable: unknown";
            return false;
        }
    }
}
