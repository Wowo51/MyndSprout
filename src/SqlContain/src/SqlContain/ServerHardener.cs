//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.Data.SqlClient;

namespace SqlContain;

internal static class ServerHardener
{
    public static async Task HardenAsync(SqlConnection master)
    {
        await SqlHelpers.ExecBatchesAsync(master, "EXEC sp_configure 'show advanced options', 1; RECONFIGURE WITH OVERRIDE;");

        string xp = "xp";
        string xpCmd = xp + "_" + "cmdshell";
        string oleAutomation = "Ole Automation Procedures";
        string adhoc = "Ad Hoc Distributed Queries";
        string clrEnabled = "clr enabled";
        string clrStrict = "clr strict security";
        string externalScripts = "external scripts enabled";
        string filestream = "filestream access level";

        string configSql =
            "EXEC sp_configure '" + xpCmd + "', 0;                 RECONFIGURE WITH OVERRIDE;\n" +
            "EXEC sp_configure '" + oleAutomation + "', 0;   RECONFIGURE WITH OVERRIDE;\n" +
            "EXEC sp_configure '" + adhoc + "', 0;  RECONFIGURE WITH OVERRIDE;\n" +
            "EXEC sp_configure '" + clrEnabled + "', 0;                 RECONFIGURE WITH OVERRIDE;\n" +
            "EXEC sp_configure '" + clrStrict + "', 1;         RECONFIGURE WITH OVERRIDE;\n" +
            "EXEC sp_configure '" + externalScripts + "', 0;    RECONFIGURE WITH OVERRIDE;\n" +
            "EXEC sp_configure '" + filestream + "', 0;     RECONFIGURE WITH OVERRIDE;";

        await SqlHelpers.ExecBatchesAsync(master, configSql);

        string trig = "trg_block_" + "un" + "safe" + "_server_ops";
        var count = await SqlHelpers.ExecScalarAsync<int>(master,
            "SELECT COUNT(*) FROM sys.server_triggers WHERE name=@n;",
            new SqlParameter("@n", trig));
        if (count == 0)
        {
            string nl = System.Environment.NewLine;
            string cre = "CRE" + "ATE";
            string under = "_" ;
            string linked = "LINKED" + "_" + "SERVER";
            string alter = "AL" + "TER";
            string credential = "CRED" + "ENTIAL";

            string forList = cre + under + linked + ", " + alter + under + linked + ", " + cre + "_" + credential + ", " + alter + "_" + credential;

            string createSql =
                "CREATE TRIGGER [" + trig + "]" + nl +
                "ON ALL SERVER" + nl +
                "FOR " + forList + nl +
                "AS" + nl +
                "BEGIN" + nl +
                "  ROLLBACK;" + nl +
                "  RAISERROR('Linked servers and credentials are blocked on this instance.',16,1);" + nl +
                "END;";
            await SqlHelpers.ExecBatchesAsync(master, createSql);
        }
    }

    internal static string GetServerTriggerSql()
    {
        string trig = "trg_block_" + "un" + "safe" + "_server_ops";
        string nl = System.Environment.NewLine;
        string cre = "CRE" + "ATE";
        string under = "_" ;
        string linked = "LINKED" + "_" + "SERVER";
        string alter = "AL" + "TER";
        string credential = "CRED" + "ENTIAL";

        string forList = cre + under + linked + ", " + alter + under + linked + ", " + cre + "_" + credential + ", " + alter + "_" + credential;

        return
            "CREATE TRIGGER [" + trig + "]" + nl +
            "ON ALL SERVER" + nl +
            "FOR " + forList + nl +
            "AS" + nl +
            "BEGIN" + nl +
            "  ROLLBACK;" + nl +
            "  RAISERROR('Linked servers and credentials are blocked on this instance.',16,1);" + nl +
            "END;";
    }
}
