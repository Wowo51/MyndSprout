# Database Development Guide

This project evolves the database with a tight feedback loop between you, ChatGPT, and the tables themselves. You donâ€™t need prior experience with MyndSprout to follow along. The idea is simple: make a small change, run it, look at the evidence, and either accept it or roll it backâ€”with backups at every step so nothing gets wrecked.

## How a run works

Each run records a row with what we expected, what we observed, whether it passed, some notes, and the model cost. Two fields matter most. **ExpectedSignal** says what success should look like (for example, â€œwe should count 7 itemsâ€). **ObservedSignal** is what actually happened. A **Pass** value of 1 means the criteria were met. We also record **LLMCost** as `LLMCost=0.xxxxxx` in the notes so we can track costs precisely and spot regressions.

## How we iterate

We start by improving the prompt that drives the agent. I paste the current prompt into ChatGPT and ask for targeted upgrades: clearer steps, cheaper tokens, and stronger guardrails. Then I run the workflow so it writes a fresh row to the runs table. Next, I copy a short, sanitized dump of the relevant rows back to ChatGPT and ask what changed, whether weâ€™re passing, and how the cost looks. If needed, I tweak the prompt again and repeat.

A change is typically accepted once we get two consecutive passing runs with non-increasing **LLMCost**. If results are erratic or cost drifts up, we revert the change and try a smaller adjustment.

## What the tables tell us

You can read the table like a lab notebook. Test names explain the intent (â€œValidate Doc 11, Run 2â€). Timestamps show the sequence. Notes capture decisions, diagnostics, and any special conditions. When something looks offâ€”like a missing **ExpectedSignal** token or a NULL observationâ€”we add a quick diagnostic run to investigate before changing anything else.

## Backups (because agents break things)

Agents are powerful and occasionally destructive. Before changing prompts, enabling auto-accept logic, or running batch edits, we take a snapshot of the database. A good snapshot includes schema, the docs, the runs, and any procedures or feature flags. Restoring is straightforward: reapply the schema, import the data, and re-run a known good validation to confirm the system is healthy again.

## Everyday rhythm

Keep edits small. After each change, run once or twice, read the table, and ask ChatGPT what it sees. Accept improvements only when the evidence is clear. If something unexpected happens, stop and back up before proceeding. Over time, these tiny, visible steps add up to a robust database that solves the math problems reliably and at a predictable cost.

