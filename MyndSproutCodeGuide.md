# MyndSprout: Developer Guide

This guide shows how to use the library in real code, with **`SqlAgent`** as the primary entry point, and the lower-level XML/string faÃ§ade (`SqlStrings`) for finer control.

---

## Quickstart

```csharp
using MyndSprout;
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        // 1) Create (if needed) and connect to a database
        var agent = await AgentFactory.CreateAgentWithDefaultServerAsync(
            dbName: "AgenticDb",
            maxEpochs: 5 // hard stop after N planning steps
        );

        // 2) Optional agent configuration (see â€œSqlAgent knobsâ€ below)
        agent.ModelKey = "gpt-5-nano";
        agent.UseIsComplete = true;            // let the agent decide when done
        agent.QueryOnly = false;               // true => read-only mode
        agent.NaturalLanguageResponse = false; // final NL synthesis
        agent.UseSearch = true;                // enable web search for planning
        agent.ReadFullSchema = false;          // true => fetch full schema upfront
        agent.KeepEpisodics = false;           // false => clear dbo.Episodics at start

        // 3) Run an objective
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        string final = await agent.RunAsync(
            prompt: "Create a Customers table if missing, then list the first 10 rows.",
            CallLog: msg => Console.WriteLine("[Agent] " + msg),
            ct: cts.Token
        );

        Console.WriteLine("\n=== Agent Final Output ===\n" + final);
    }
}
```

---

## Creating an `SqlAgent`

Create instances via **`AgentFactory`**:

* **Create (if missing) & connect using a server connection**

  ```csharp
  var agent = await AgentFactory.CreateAgentAsync(
      serverConnectionString: "Server=localhost;Integrated Security=True;TrustServerCertificate=True",
      dbName: "AgenticDb",
      maxEpochs: 5
  );
  ```

* **Connect to an existing DB with a full connection string**

  ```csharp
  var agent = await AgentFactory.CreateAgentFromConnectionStringAsync(
      connectionString: "Server=localhost;Database=AgenticDb;Integrated Security=True;TrustServerCertificate=True",
      maxEpochs: 5
  );
  ```

* **Use default server (great for local dev / (localdb)\MSSQLLocalDB)**

  ```csharp
  // Env overrides honored: AGENTICSQL_SERVER_CONNSTR or AGENTICSQL_CONNSTR
  var agent = await AgentFactory.CreateAgentWithDefaultServerAsync("AgenticDb", maxEpochs: 5);
  ```

---

## Running the Agent

```csharp
string output = await agent.RunAsync(
    prompt: "Import CSV files from ./data into tables and summarize totals per month.",
    CallLog: s => System.Diagnostics.Debug.WriteLine(s),
    ct: CancellationToken.None
);
```

**How each epoch works:**

1. **Context prep**

   * If `ReadFullSchema == true`: fetch full schema via `GetSchemaAsyncStr("<Empty/>")` and wrap as `<CurrentSchema>â€¦</CurrentSchema>`.
   * Else: include a short note telling the model to explicitly request schema and **never guess** names.
2. **Plan**: ask the LLM to return a strict `<SqlXmlRequest>` payload.

   * If `UseSearch == true`, the planning call goes through **`LLM.SearchQuery(...)`**.
   * Otherwise it goes through **`LLM.Query(...)`**.
3. **Execute**: send the `<SqlXmlRequest>` to `ExecuteToXmlAsync(...)`.
4. **Schema refresh (conditional)**:

   * If `ReadFullSchema == true`: refresh schema again.
   * Else if `<SqlXmlRequest>` set `IsSchemaRequest=true`: take the execution output as the new schema snapshot for next epoch.
5. **Episodic**: synthesize a rolling StateOfProgress/NextStep and persist to `dbo.Episodics`.
6. **Done check**: if `UseIsComplete == true`, ask the LLM to return `<Done>true|false</Done>`; stop early on `true`.

**Return value**:

* If `NaturalLanguageResponse == false` (default), you get a **Final State** text block containing the last query XML (if any).
* If `true`, the agent does a final `LLM.Query(...)` pass to produce a concise **plain-text** answer.

---

## Internet Search (planning only)

When **`UseSearch = true`**, the planning step uses **`SwitchLLM.LLM.SearchQuery(...)`**. Execution against SQL is unchanged.

> The planning prompt contains the phrase â€œwrite the term `UseSearch`â€¦â€. Internally, the agent already routes to **Search** whenever `UseSearch` is on; you donâ€™t need to add anything else.

---

## `SqlAgent` knobs (properties)

```csharp
agent.ModelKey = "gpt-5-nano";              // (forwarded by your LLM layer if supported)
agent.UseIsComplete = true;                 // ask LLM <Done>true/false</Done>
agent.QueryOnly = false;                    // true => hard read-only enforcement
agent.NaturalLanguageResponse = false;      // final natural-language synthesis
agent.MaximumLastQueryOutputLength = 25_000;// size guard when echoing last results
agent.UseSearch = true;                     // use web search for planning
agent.ReadFullSchema = false;               // true => fetch full schema every epoch
agent.KeepEpisodics = false;                // false => clear dbo.Episodics at run start
```

### Read-Only Mode (`QueryOnly = true`)

* Blocks anything that could change state.
* Enforcement:

  * Requires `CommandType == Text`.
  * Runs **`MyndSprout.Security.SqlMutationScanner`** (detects DML/DDL/temp tables/transactions/perms/external, etc.).
  * If risky â†’ the step is rejected.
* **Return behavior** in read-only mode: the agent **executes allowed queries** and **returns the SQL result XML immediately** (it does **not** return the raw `<SqlXmlRequest>`).

Use this for dashboards, UIs, or initial exploration of production data.

---

## Whatâ€™s persisted in `dbo.Episodics`?

One row per epoch:

* `EpisodeId` (guid), `EpochIndex`, `Time`
* `PrepareQueryPrompt` (full planning prompt shown to LLM)
* `QueryInput` (the `<SqlXmlRequest>` returned by LLM)
* `QueryResult` (the `<SqlResult>` XML from execution)
* `EpisodicText` (rolling StateOfProgress / NextStep)
* `DatabaseSchema` (schema snapshot or guidance text when `ReadFullSchema=false`)
* `ProjectId` (int)

The table is **auto-created**. It is **cleared at run start only when** `KeepEpisodics == false`.

---

## Advanced: the XML/string faÃ§ade (`SqlStrings`)

Everything the agent does is available directly via **`SqlStrings`**.

### Connect

```csharp
var sql = new SqlStrings();
string connectXml =
  $@"<ConnectInput><ConnectionString>{System.Security.SecurityElement.Escape(connStr)}</ConnectionString></ConnectInput>";
string result = await sql.ConnectAsyncStr(connectXml); // <Result success="true" ...
```

### Get schema as structured XML

```csharp
string schemaXml = await sql.GetSchemaAsyncStr("<Empty/>");
```

### Execute text SQL (query or non-query)

```csharp
var req = new SqlStrings.SqlTextRequest {
    Sql = "SELECT TOP (10) name, object_id FROM sys.tables ORDER BY name",
    ExecutionType = SqlStrings.SqlExecutionType.Query
};
string xml = await sql.ExecuteAsync(req); // <Result success="true"><Rows>...
```

**String-in helper:**

```csharp
string inXml = """
<SqlTextRequest>
  <Sql>UPDATE dbo.Customers SET IsActive = 1 WHERE Id = @id</Sql>
  <ExecutionType>NonQuery</ExecutionType>
  <Parameters>
    <NameValue><Name>@id</Name><Value>42</Value></NameValue>
  </Parameters>
</SqlTextRequest>
""";
string outXml = await sql.ExecuteAsyncStr(inXml);
```

### Execute a full `<SqlXmlRequest>`

```csharp
var x = new SqlXmlRequest {
    Sql = "SELECT TOP (3) * FROM sys.databases",
    CommandType = System.Data.CommandType.Text
};
string xml = await sql.ExecuteToXmlAsync(x); // returns <SqlResult> ... </SqlResult>
```

**String-in:**

```csharp
string inXml = """
<SqlXmlRequest>
  <Sql>EXEC sys.sp_who</Sql>
  <CommandType>StoredProcedure</CommandType>
  <Parameters>
    <XmlSerializableSqlParameter>
      <ParameterName>@RETURN_VALUE</ParameterName>
      <Direction>ReturnValue</Direction>
      <SqlDbType>Int</SqlDbType>
    </XmlSerializableSqlParameter>
  </Parameters>
</SqlXmlRequest>
""";
string xml = await sql.ExecuteToXmlAsyncStr(inXml);
```

---

## Schema/XSD helpers (great for LLM prompts)

```csharp
string xsd  = InputXmlSchemas.SqlXmlRequestXsd();   // XSD for <SqlXmlRequest>
string xsd2 = InputXmlSchemas.SqlTextRequestXsd();  // XSD for <SqlTextRequest>
var all     = InputXmlSchemas.All();                // Dictionary<string,string> of all XSDs
```

General helpers in `Common`:

* `Common.ExtractXml(string)` â€“ pulls the first XML blob from noisy text.
* `Common.FromXml<T>(string)` â€“ deserialize if valid.
* `Common.ToXmlSchema<T>()` â€“ generate an XSD for a .NET type (adds list/array notes).

---

## Security posture

* Agent never asks SQL to do external IO (files/network/CLR/`xp_cmdshell`, etc.).
* With **`UseSearch = true`**, only the **planning** call may use the web; SQL execution stays local.
* In **read-only** mode:

  * Forces `CommandType=Text`.
  * Scans the SQL with `SqlMutationScanner`.
* Still follow best practices:

  * Least-privilege DB credentials (e.g., `SELECT` only for read-only).
  * Consider a rollback-only transaction sandbox for ad-hoc exploration.
  * Use `TrustServerCertificate=True` only where appropriate.

---

## Cancellation & logging

* `RunAsync` accepts a `CancellationToken`. Cancelling mid-epoch will throw and stop the agent.
* All major steps are surfaced through `CallLog(string)` for UI/console tracing.

---

## Troubleshooting

* **Malformed XML from LLM/Search**
  The agent uses `Common.FromXml<T>` with `ExtractXml` to be tolerant. Show the XSD (`InputXmlSchemas.SqlXmlRequestXsd()`) in prompts and require â€œReturn ONLY one well-formed `<SqlXmlRequest>...</SqlXmlRequest>`.â€

* **Read-only block**
  If you see: â€œRead-only mode: Potentially mutating SQL detected; blocked.â€ â†’ The mutation scanner matched DML/DDL/etc. Rephrase the objective for descriptive analytics or disable `QueryOnly` (not recommended on prod).

* **Schema handling confusion**
  Set `ReadFullSchema = true` to always fetch a fresh snapshot each epoch. Otherwise, explicitly request schema via `<SqlXmlRequest IsSchemaRequest="true">`.

* **Local dev connection**
  Defaults to `(localdb)\MSSQLLocalDB` with Integrated Security. Override via `AGENTICSQL_SERVER_CONNSTR` / `AGENTICSQL_CONNSTR` or pass a specific connection string to the factory.

---

## Minimal task patterns

**List tables (read-only + search):**

```csharp
agent.QueryOnly = true;
agent.UseSearch = true;
string final = await agent.RunAsync(
    "Show all tables and their row counts. Use best-practice queries.",
    Console.WriteLine
);
```

**Create table then query (no search):**

```csharp
agent.QueryOnly = false;
agent.UseSearch = false;
string final = await agent.RunAsync(
    "If dbo.Customers is missing, create it (Id int PK, Name nvarchar(200)); then select TOP(5) *.",
    Console.WriteLine
);
```

**Natural-language final answer + search:**

```csharp
agent.NaturalLanguageResponse = true;
agent.UseSearch = true;
string final = await agent.RunAsync(
    "Summarize total sales by month for 2024 and provide a short plain-text explanation.",
    Console.WriteLine
);
```

**Drive DB directly (no agent loop):**

```csharp
var sql = new SqlStrings();
await sql.ConnectAsyncStr($@"<ConnectInput><ConnectionString>{System.Security.SecurityElement.Escape(connStr)}</ConnectionString></ConnectInput>");
string result = await sql.ExecuteAsyncStr("""
<SqlTextRequest>
  <Sql>SELECT TOP(10) name FROM sys.tables ORDER BY name</Sql>
  <ExecutionType>Query</ExecutionType>
</SqlTextRequest>
""");
```

---

## Notes

* Target framework: **.NET 9.0**
* ADO provider: **Microsoft.Data.SqlClient 6.1.1**
* LLM integration: **`SwitchLLM.LLM.Query(...)`** and **`SwitchLLM.LLM.SearchQuery(...)`** are used by the agent. (The `ModelKey` is exposed on `SqlAgent`; pass/consume it per your `SwitchLLM` implementation if desired.)

