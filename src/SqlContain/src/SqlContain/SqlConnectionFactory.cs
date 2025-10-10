//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.Data.SqlClient;

namespace SqlContain;

internal static class SqlConnectionFactory
{
    public static SqlConnection CreateMaster(HardenerOptions o)
    {
        SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
        csb.DataSource = o.Server ?? string.Empty;
        csb.InitialCatalog = string.IsNullOrEmpty(o.Database) ? "master" : o.Database;

        if (string.Equals(o.Auth, "Trusted", StringComparison.OrdinalIgnoreCase))
        {
            csb.IntegratedSecurity = true;
        }
        else if (string.Equals(o.Auth, "Sql", StringComparison.OrdinalIgnoreCase))
        {
            csb.IntegratedSecurity = false;
            csb.UserID = o.User ?? string.Empty;
            csb.Password = o.Password ?? string.Empty;
        }
        else
        {
            csb.IntegratedSecurity = true;
        }

        SqlConnection conn = new SqlConnection(csb.ConnectionString);
        return conn;
    }
}
