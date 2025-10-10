//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlTools;

namespace MyndSproutApp.Actions
{
    public sealed class ExportSchemaAction
    {
        private readonly Action<string> _log;
        public ExportSchemaAction(Action<string> log) => _log = log ?? (_ => { });

        public async Task RunAsync(MainViewModel vm)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));

            string connStr = ConnectionStringHelper.BuildFromViewModel(vm);
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            _log("ExportSchema: generating schema SQL ...");
            string sql = ExportSchema.Export(conn);

            LogHelpers.LogBlock(_log, "ExportSchema", sql);
        }
    }
}
