# MyndSprout App: Help & User Guide

Welcome! This guide walks you through the **MyndSprout** desktop app (WPF, .NET 9). It covers setup, the main workflow, safety (read-only + containment), import/export (including schema **XML**), the **web search** option, and troubleshooting.

---

## What the app does

* Creates or connects to a SQL Server database (defaults to LocalDB).
* Runs an iterative SQL agent that plans and executes T-SQL in short “epochs.”
* Shows a live log with Start/Stop and prompt open/save helpers.
* Optional containment hardening (SqlContain).
* Optional **web search** to improve planning (no external IO inside SQL).
* Utilities to import a folder into a table, and to export schema (DDL) or data (XML), and **schema as XML**.

---

## First-run setup

### 1) LLM API keys (required: at least one)

Open `MainWindow.xaml.cs` and set one or both:

```csharp
OpenAILLM.LLM.OpenAiKeyPath = @"C:\path\to\openai.txt";
OpenRouter.LLM.KeyPath       = @"C:\path\to\openrouter.txt";
OpenRouter.LLM.Initialize();
```

If **both** paths are empty, the app throws:

> “No key path set. It goes just above this. Set the defaults in LLMSwitch too.”

Put a valid key in each file you reference.

### 2) SQL Server

If you don’t specify anything, the app uses LocalDB with:

```
Server=(localdb)\MSSQLLocalDB;Database=<Db Name>;Trusted_Connection=True;TrustServerCertificate=True;
```

The **Db Name** toolbar field controls the database name (default shown is **SelfImproving2**). The app creates the DB if it’s missing.

---

## The Main Window

**File ▾**
Open Prompt… (load `.txt`/`.prompt`), Save Prompt, Save Prompt As…

**StartAgent / StopAgent**
Start runs the agent with your current Prompt and settings. Stop cancels via a `CancellationToken` and reliably re-enables Start.

**MaxEpochs**
Hard cap on planning steps (default 10).

**UseIsComplete**
Let the agent stop early when objectives are met (<Done>true</Done> check).

**QueryOnly**
Enforce read-only SQL (no DML/DDL; mutation scanner blocks risky SQL). See **Safety & read-only**.

**NaturalLanguageResponse**
Adds a final LLM pass for a concise plain-text answer.

**Db Name**
Database to create/connect (default SelfImproving2).

**CopyLog / ClearLog / ClearAllButLast**
Log utilities. *CopyLog* wraps the entire log in a `<Log>…</Log>` XML tag for easy pasting elsewhere.

**ImportFolder / ExportData / ExportSchema / ExportSchemaXml**
One-click utilities (see **Import / Export** below).

**ModelKey**
Advanced. Present in the ViewModel but hidden in the UI (collapsed). Developers can unhide it in XAML to change per run.

**ContainServer / ContainDatabase**
Run SqlContain hardening before starting. If hardening fails, the run is aborted (fail-closed).

**UseSearch**
Allow the agent to use web search during **planning** (execution stays local).

### Panes

* **Input** (left): your Prompt (plain text, multi-line).
* **Output (most recent lines)** (right): rolling window of the latest log lines; the box auto-scrolls to the end.

An **Epoch N / MaxEpochs** readout updates as the agent logs “Epoch N starting.”

---

## Typical workflow

1. **Write your Prompt**
   Describe the goal and (optionally) a sequence of epochs. Example:

> Ensure a `Memory` table exists with columns `Name` and `Content`.
> Load existing rows to avoid duplicates.
> Insert three long articles about safety principles.
> Finally, select all rows ordered by creation time.

2. **Pick settings**

* MaxEpochs: start with 5–10
* UseIsComplete: on if your prompt clearly defines “done”
* QueryOnly: on for analytics-only; off if schema/data changes are expected
* NaturalLanguageResponse: on if you want a plain-text summary
* Containment: enable if you have permissions and want guardrails
* UseSearch: enable if an external reference might help planning

3. **StartAgent**
   The log shows each epoch’s planning prompt and execution output. With UseSearch on, the agent may switch to its search-capable LLM call during planning.

4. **StopAgent (optional)**
   Cancels the run safely and logs “Agent canceled.”

5. **Review results**
   You’ll see either the **Final State** text (default) or a **plain-text summary** if NaturalLanguageResponse is enabled.

---

## Safety & read-only

Turn **QueryOnly** on to force read-only analytics:

* The agent must avoid mutating SQL.
* A mutation scanner (comment/string-aware) checks for:

  * DML: `INSERT/UPDATE/DELETE/MERGE`, `OUTPUT INTO`, `BULK INSERT`
  * DDL: `CREATE/ALTER/DROP/TRUNCATE`, `SELECT … INTO`, temp tables / table variables
  * `EXEC/sp_executesql/xp_*`, permissions (`GRANT/REVOKE/DENY`)
  * Transactions, `DBCC`, backup/restore, `USE db`, index maintenance, linked/external servers
* Risky SQL is blocked.

**Return behavior in read-only mode:** the agent **executes allowed queries** and returns the **SQL result XML** directly (it does not wrap in the “Final State” block while QueryOnly is true).

**Tip:** Combine QueryOnly with **ContainServer/ContainDatabase**. If SqlContain hardening fails, the app aborts the run.

---

## Web Search (planning only)

When **UseSearch** is enabled:

* The planning step may use the LLM’s search-capable endpoint to avoid guesswork (e.g., canonical window-function patterns).
* SQL **execution** remains local to your database. No external IO inside SQL is allowed.
* The log will show the usual planning prompts and results; it doesn’t promise a narrative of search rationales.

Use this when you want the planner to verify/recall standard T-SQL idioms. Turn it off for lowest latency.

---

## Database connection logic

In this UI, you provide **Db Name**. The app resolves the connection as:

1. If a full `ConnectionString` exists in the ViewModel, use it (hidden in this UI).
2. Else if a `ServerConnectionString` exists, append `Database=<Db Name>` if missing (hidden in this UI).
3. Else default to LocalDB + trusted connection.

On StartAgent, the app ensures the database exists, then connects.

---

## Containment (SqlContain)

If **ContainServer** or **ContainDatabase** is checked:

* The app derives server/auth/db from the same connection logic as above.
* Scope is chosen:

  * Both (if both boxes checked),
  * Instance,
  * or Database.
* The hardener runs before the agent. A non-zero exit or exception is logged and the run is aborted. You’ll need the necessary SQL permissions (often sysadmin).

---

## Import / Export

**ImportFolder**

* Choose a folder. All files (recursively) are imported into `dbo.<FolderName>` (sanitized to a valid identifier).
* Columns include `Name` (relative path), `Time` (UTC), and `Content` (text).
* The importer creates the table if needed. Great for ad-hoc text analytics.

**ExportSchema**

* Generates DDL via `SqlTools.ExportSchema.Export(conn)` and logs it in one block.

**ExportData**

* Exports data as XML via `SqlTools.ExportDataXml.Export(conn, includeEmptyTables: true)` and logs it in one block.

**ExportSchemaXml**

* Connects via `SqlStrings`, calls `GetSchemaAsyncStr("<Empty/>")`, and logs the schema as structured **XML** in one block.

---

## Logging

* The app buffers many lines but shows only the **latest slice** for responsiveness.
* Large outputs (DDL/XML) are logged **as a single block** bracketed by `BEGIN/END` lines (not chunked per N characters).
* **CopyLog** copies the **entire** buffer, wrapped in `<Log>…</Log>`.
* **ClearLog** wipes the buffer. **ClearAllButLast** keeps only the final line.

---

## Tips for effective prompts

* Be goal-oriented: “Create X if missing, then do Y, finally summarize Z.”
* Keep intermediate result sets small (TOP, WHERE, sensible ORDER BY).
* For production exploration, enable QueryOnly and request summaries.
* If you want the planner to verify a tricky idiom (e.g., “greatest-N-per-group with ties”), enable **UseSearch** and mention the pattern to verify.

---

## Troubleshooting

* **“No key path set …” at startup**
  Set `OpenAiKeyPath` and/or `OpenRouter.KeyPath` to files that contain valid API keys; call `OpenRouter.LLM.Initialize()` (already in code).

* **Agent won’t start with containment on**
  Check SQL permissions and the scope you selected. Fail-closed behavior is by design.

* **“Read-only mode: Potentially mutating SQL detected; blocked.”**
  Rephrase to analytics-only, or disable QueryOnly (not recommended against production).

* **Output looks truncated**
  The Output pane shows a rolling window. Use **CopyLog** to capture the entire run.

* **Search feels slow**
  Web search adds latency and can occasionally fail; the agent continues. Turn off **UseSearch** if you prefer the lowest latency.

---

## Power-user notes

* `ModelKey` is surfaced on the ViewModel (default `gpt-5-mini`) but the UI control is collapsed; unhide in XAML to edit at runtime.
* The agent records per-epoch rows in `dbo.Episodics` (plans, inputs, results, notes, schema snapshot).
* Read-only mode enforces `CommandType=Text` and runs a pragmatic T-SQL mutation scanner.
* NaturalLanguageResponse switches the final return to a concise plain-text answer.
* UseIsComplete asks the LLM for `<Done>true</Done>` and may stop before MaxEpochs.

---

## Example scenarios

**Safe analytics on production**

* Enable **QueryOnly** (and optionally containment).
* Prompt: “Summarize total sales by month for 2024 and list top 10 customers with totals.”

**Import a docs folder and analyze**

* Use **ImportFolder** on your repo/docs folder.
* Prompt: “Count files mentioning ‘zero-trust’ (case-insensitive) and list the top 20 file names by occurrences.”

**Verify a tricky SQL pattern with web search**

* Enable **UseSearch**.
* Prompt: “Verify canonical ‘greatest-N-per-group with ties’ using window functions, then implement for `Sales` per `CustomerId`.”

**Export for migration**

* Use **ExportSchema** for DDL and **ExportData** for small XML snapshots.
* Use **ExportSchemaXml** to capture a structured schema **XML** view.

---

## Final notes

* Target framework: `net9.0-windows` (WPF).
* Defaults favor developer convenience (LocalDB + trusted connection).
* For production: least privilege, **QueryOnly**, SqlContain hardening, and enable **UseSearch** only when it adds value.
