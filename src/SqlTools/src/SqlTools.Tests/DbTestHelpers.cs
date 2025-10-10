//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.Data.SqlClient;

namespace SqlTools.Tests
{
    internal static class DbTestHelpers
    {
        internal static string GetMasterConnectionString() => "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;";

        internal static string GetTestDbConnectionString(string dbName) => $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Integrated Security=true;TrustServerCertificate=true;";

        internal static void CreateDatabase(string dbName)
        {
            if (string.IsNullOrWhiteSpace(dbName)) throw new ArgumentException(nameof(dbName));
            using var conn = new SqlConnection(GetMasterConnectionString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}];";
            cmd.ExecuteNonQuery();
        }

        internal static void DropDatabase(string dbName)
        {
            if (string.IsNullOrWhiteSpace(dbName)) return;
            using var conn = new SqlConnection(GetMasterConnectionString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"IF DB_ID(N'{dbName}') IS NOT NULL ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}];";
            cmd.ExecuteNonQuery();
        }
    }
}
