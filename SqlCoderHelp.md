# SqlCoder Help - MyndSprout's SQL Generation Agent

Welcome to the MyndSprout SqlCoder! This document explains how to effectively use our AI agent, which is designed to *continuously learn and improve its SQL coding abilities* through a unique self-improvement process based on a minimalist implementation of ACE(Agentic Context engineering)..

## Interacting with MyndSprout: Your Prompt, Your Task

You'll interact with MyndSprout through a Graphical User Interface (GUI) by providing a **prompt**. This prompt is the AI's core instruction set, defining its mission and operational rules.

The agent starts with a comprehensive **base prompt**, like the one provided below, which outlines its primary goal, allowed actions, constraints, and how it learns.

### The Base Prompt (AI's Operating Instructions)

```markdown
TITLE: Self-Improvement Sprint — SQL Skill Growth (ACE-Lite)

PRIMARY GOAL
Continuously improve SQL coding quality and problem-solving ability using tiny, safe iterations. Each epoch: plan → act → observe → summarize → heal. Persist lessons as nodes; rank and reuse them.

OBJECTS
Tables: dbo.MemoryNodes, dbo.MemoryEdges, dbo.KV
Procs: dbo.usp_UpsertNode, dbo.usp_AddEdge, dbo.usp_GetRanked

STRICT CONSTRAINTS
• Return ONLY one <SqlXmlRequest>… per step; no commentary.
• If object existence is unknown, FIRST request schema (IsSchemaRequest=true).
• No GO; no external IO; prefer parameters; narrow result sets.
• Idempotent DDL/DML; never DROP.
• Do not write directly to dbo.Episodics.
• In read-only mode: return a diagnostic SELECT and stop the mutation.

METRICS (write/refresh Metric nodes; keep values small & comparable)
• ErrorRate = fraction of last 10 executions with <Error>.
• NullRate  = fraction of non-error executions with empty/mostly-null results.
• ParamRate = fraction of queries using parameters.
• PatternReuse = fraction of epochs that cite an existing SqlPattern or Rule.
• Complexity = ladder (1..5): 1 simple SELECT; 2 WHERE/ORDER; 3 GROUP BY; 4 JOIN+window; 5 CTE/multi-step.

SKILL LADDER (advance one step only when ErrorRate <= 0.2 for the last 10)
1. Safe parameterized SELECTs (filters, small outputs).
2. Aggregations & GROUP BY with HAVING.
3. Multi-table JOINs (inner/left) using keys; avoid cartesian products.
4. Window functions (ROW_NUMBER, LAG/LEAD) for ranking and trends.
5. CTEs / staged queries; optional tiny, safe indexes only if clearly justified.

EPOCH LOOP (one tiny step per epoch)
A) PLAN
Use usp_GetRanked(@Query in ('roadmap','error','pattern','ranking','ladder')), pick ONE smallest improvement aligned with the ladder and metrics.

B) ACT
Do exactly one of:
• Add/Refine SqlPattern (idiom or fix);
• Add Observation (what happened, 1–3 lines);
• Adjust a Rule (safety/quality);
• Execute a tiny practice query at your current ladder step (parameterized; minimal rows).
Link with usp_AddEdge(Type='Uses'/'Supports').

C) OBSERVE
From the execution result: write 1 Observation; if a pattern helped, bump its Score slightly; if it hurt, decrement slightly.

D) SUMMARIZE
Maintain one Summary “SIS Summary vN”: what changed, current ladder step, metrics snapshot, next tiny step.

E) HEAL
On repeated <Error>/empty output: request schema; reduce scope; fix join keys; add/adjust parameters; capture an ErrorPattern with “how to avoid”.

SEED (if empty; do once)
• Task “SQL Self-Improvement v1”: follow ladder; reduce ErrorRate; increase ParamRate & PatternReuse.
• Rule “Safety v1”: tiny/idempotent, never DROP, schema-first when unsure.
• Rule “Loop-Breaker v1”: change approach after repeated failure.
• SqlPattern “Param v1”: always parameterize user-supplied values.
• Verify: usp_GetRanked('roadmap', 5).

STOP (for <Done>true/>)
1. Two or more ladder steps completed with ErrorRate <= 0.2 and ParamRate >= 0.8 over last 10.
2. PatternReuse >= 0.5 over last 10.
3. Current Summary lists next concrete step and the present ladder level.
---
Tips:
* To “turn up the heat,” swap the `@Query` focus (e.g., "join", "window", "ctes").
* If you want math tasks later, just set a new Task node (e.g., “Compute moving median per day”)—the same loop will learn on that domain.
```

### Appending Your Own Tasks

**The recommended way to use MyndSprout is to append your specific SQL task directly to this base prompt.**

When you add your task, the AI's internal processes, guided by ACE-Lite, will begin to work on fulfilling your request *while simultaneously continuing its self-improvement journey*. This means your task implicitly influences the AI's "Plan" phase, potentially leading it to create new `SqlPattern` nodes or reuse existing ones to solve your problem. You can also recommend that your task be prioritized over improving sql coding ability.

**Example of how you would append a task:**

```markdown
... [The entire base prompt above] ...

// Your specific task goes here:
Generate a SQL query to retrieve the top 5 most frequently used SqlPatterns from the dbo.MemoryNodes table based on their `Score` and linked `Observation` count, along with their associated `Content`.
```

By presenting your needs this way, the AI integrates it into its learning loop.

## What is ACE-Lite and How Does it Learn?

ACE-Lite is the engine behind MyndSprout's continuous improvement.

Here’s a breakdown for coders to understand the `SqlCoder`'s self-improving nature:

### The PAOSH (Plan, Act, Observe, Summarize, Heal) Loop

This iterative cycle is how the AI operates and learns:

1.  **Plan:** The AI examines its current state, past observations, and internal metrics (like `ErrorRate` and `Complexity`) to decide on the single best next action. This might be to tackle a part of your appended task, or to perform a "practice query" to advance its general SQL skills.
2.  **Act:** It performs precisely one action. This often involves generating and executing a small SQL query, refining an internal `SqlPattern`, or recording a new `Observation`.
3.  **Observe:** After an action, the AI records the outcome as an `Observation` node. Crucially, if a specific `SqlPattern` or `Rule` was used, its internal `Score` is adjusted based on success (`+0.1`) or failure (`-0.1`). This is how it learns *what works*.
4.  **Summarize:** A special `Summary` node is updated with a snapshot of the AI's most recent performance metrics and its calculated current "Skill Ladder" level. It also suggests what the next conceptual step might be.
5.  **Heal:** If the AI encounters repeated errors or unexpected results, it initiates diagnostic steps. This could involve requesting schema information for a table it's unfamiliar with, narrowing its query scope, or even creating an `ErrorPattern` to remember how to avoid that specific issue in the future.

### Semantic Memory: The AI's SQL Brain

All of the AI's knowledge, experiences, and rules are stored as "nodes" in a SQL Server database, acting as its persistent memory.

*   **`SqlPattern`**: These are the fundamental units of SQL knowledge. They are idiomatic SQL snippets or query structures that the AI has learned and can reuse. For example, a `SqlPattern` might store the structure for a `GROUP BY` clause or a parameterized `LIKE` query.
*   **`Observation`**: These nodes are essentially log entries, detailing the results of the AI's actions. An `Observation` might record "returned 12 rows" or "encountered syntax error."
*   **`Rule`**: These nodes enforce safety guidelines and best practices, such as "never DROP tables" or "always parameterize user input."
*   **`Summary`**: A single, dynamic node that provides a high-level overview of the AI's current learning state, including its `Skill Ladder` progress and key performance metrics.

### Linking and Scoring: Reinforcement Learning

The `dbo.MemoryEdges` table tracks relationships between these memory nodes. When the AI uses a `SqlPattern` to generate a query that results in a successful `Observation`, a "Uses" edge is created. The `SqlPattern`'s internal `Score` is then increased, making it more likely to be chosen for future tasks. Conversely, if a pattern leads to an error, its score decreases. This process is a simple form of reinforcement learning, constantly refining the AI's SQL generation capabilities.

### Metrics & the Skill Ladder

The AI constantly monitors itself using metrics calculated from its last 10 `Observation` nodes:

*   **`ErrorRate`, `ParamRate`, `PatternReuse`**: These quantitative measures guide the AI's self-improvement.
*   **`Complexity` (Skill Ladder 1-5)**: The AI progresses through a defined sequence of SQL complexities (from simple `SELECT`s to advanced CTEs). It only advances each "ladder" step when its `ErrorRate` is sufficiently low, ensuring a solid foundation before moving to more advanced topics.

**This continuous feedback loop allows the agent to adapt automatically across epochs, improving its SQL generation over time, much like a human developer gaining experience.**

## For Coder: Observing the AI's Learning

We provide access to the AI's internal database (containing `dbo.MemoryNodes`, `dbo.MemoryEdges`, and other relevant tables) so you can directly observe its learning process.

By querying these tables, you can:
*   See the `SqlPattern` nodes it has learned and their current `Score`s.
*   Review the `Observation` nodes to track its progress and identify where it succeeded or struggled.
*   Inspect the `Summary` node to understand its current skill level and next objectives.

This transparency allows you to understand not just *what* SQL the AI produces, but *how* it arrived at that solution and *how it's evolving*.