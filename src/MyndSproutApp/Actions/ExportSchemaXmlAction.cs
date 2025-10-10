//MIT license with supplement...
using System;
using System.Threading.Tasks;
using MyndSprout;

namespace MyndSproutApp.Actions
{
    public sealed class ExportSchemaXmlAction
    {
        private readonly Action<string> _log;
        public ExportSchemaXmlAction(Action<string> log) => _log = log ?? (_ => { });

        public async Task RunAsync(MainViewModel vm)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));

            string connStr = ConnectionStringHelper.BuildFromViewModel(vm);
            var sql = new SqlStrings();

            // Connect using the same string the app uses elsewhere
            string connXml =
                $"<ConnectInput><ConnectionString>{System.Security.SecurityElement.Escape(connStr)}</ConnectionString></ConnectInput>";
            var connectResult = await sql.ConnectAsyncStr(connXml);

            _log("ExportSchemaXml: connected. Fetching schema XML...");
            string xml = await sql.GetSchemaAsyncStr("<Empty/>");

            LogHelpers.LogBlock(_log, "ExportSchemaXml", xml);
        }
    }
}

