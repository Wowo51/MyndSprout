//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlTools;

namespace MyndSproutApp.Actions
{
    public sealed class ExportDataAction
    {
        private readonly Action<string> _log;
        public ExportDataAction(Action<string> log) => _log = log ?? (_ => { });

        public async Task RunAsync(MainViewModel vm, bool includeEmptyTables = true)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));

            string connStr = ConnectionStringHelper.BuildFromViewModel(vm);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            _log("ExportData: exporting data as XML ...");
            string xml = ExportDataXml.Export(conn, includeEmptyTables);

            LogHelpers.LogBlock(_log, "ExportDataXml", xml);
        }
    }
}
