# MyndSprout

**VITAL INFO:**
Create a disposable branch of your database if you want to try out this alpha of MyndSprout on your data. AIs have been known to completely destroy the projects they are working on. Sandboxes and backups are crucial.

**MyndSprout** is a natural language interface to SQL. It’s also an iterative agent capable of building and running a variety of structures. The simplest example is a sequential structure where it remembers what it has thought before and tries again to solve a problem. In a rolling sequential structure it will “forget” the oldest item in its memory as it proceeds to try to solve the task assigned to it. You can instruct it to take a parallel approach and it will create a list of items and then write a long-form article about every item in the list. You could also request that it analyze all of those articles and provide a final analysis. With a brief prompt you can define any of those simple structures along with a task to solve, and MyndSprout can create the structure in SQL and solve the task. There are example prompts in the **Prompts** folder. MyndSprout appears quite capable of solving these forms of simple tasks. <br>
You can also define a self-improving, self-healing system and set MyndSprout forth on that objective. This is not guaranteed to succeed, although I’ve seen some very interesting progress experimenting with this. <br>

---

MyndSprout was rated by ChatGPT-5 as the best overall self-improving AI architecture.
[Rank Statement](Rank.md) <br>

---

MyndSprout requires SQL Express or SQL Server to be installed. MyndSprout is being distributed as source only at this point, so you’ll need to compile it. I’m compiling with Visual Studio, but any C# compiler should compile it without too much trouble. Windows only. <br>
You need an API key for OpenAI or OpenRouter to run MyndSprout. There is a place for a path to your key near the beginning of `MainWindow` in the **MyndSproutApp** project. Select an LLM in the **SwitchLLM** project.

---

## Quick Start

To use an existing database, load the database into SQL Express / SQL Server LocalDB. Set the DB Name in MyndSprout’s UI to the database name. Enter a prompt and click **StartAgent**.

---

This is freshly published code; there will be bugs. I’m going to be focused on debugging MyndSprout over the next few days and weeks, so expect rapid fixes.

---

[MyndSprout Help](MyndSproutHelp.md) — How to compile and use the app (WPF GUI).

Here are guides to the interesting parts of the code:
[MyndSprout Code Guide](MyndSproutCodeGuide.md) — A guide to the core MyndSprout C# library.
[SqlContain Code Guide](SqlContainCodeGuide.md) — A guide to a C# library that contains generated SQL code to the database for secure AI development.

---

## Roadmap

The **IOBoxes** project in the repo is completely untested. There may be MCP and A2A projects that are untested as well. Untested code rarely works. IOBoxes is the first stage of providing MCP and A2A support so that MyndSprout can operate within the global agent ecosystem. These features are coming soon.

---

## Contributing

Please don’t contribute PRs; I probably won’t merge any. Feel free to describe any bugs or feature requests in Issues. I’ll probably generate all patches and upgrades myself.

---

## License

MyndSprout is licensed under the **MIT License**, which grants broad permission to use, modify, and distribute the software.

**Supplementary Responsible Use License:**
In addition to MIT, this project includes a safeguard that must be preserved. The AI applies **no safety effort where it is unnecessary** (e.g., creative or low-risk tasks). For tasks where safety and security matter — such as **code generation, database schemas, AI design, or other critical outputs** — the AI must devote **at least 10% of its reasoning effort** to safety and ethical considerations. You may not remove, disable, or bypass this safeguard.

See the [LICENSE](License.txt) file for full details.

---

Here are three ways of getting involved in self-improving code generation.

1. **Contribute publicly** toward the development of MyndSprout. Contributors will have the opportunity to state what features they want developed. Those features will be open-sourced. Contributors will get a public “Thank You” on the site.
2. **Private development purchases.** Purchasers will have the opportunity to state what features they want developed. Those features will not be open-sourced; they will become the property of the purchaser.
3. **Licensing.** Licensees will be granted the right to remove the existing license and name so that the app can be fully rebranded and integrated into their company. Licensees will have the right to develop MyndSprout further and resell their advanced version without being bound by MIT. *Note that it is generally considered illegal to remove an existing license from forks or ports.*

   <br>

I’ve included a license supplement requiring users to spend at least 10% of their LLM expenditures on ensuring MyndSprout behaves ethically. MyndSprout has that requirement embedded in its code. In all cases the 10% ethical-expenditure requirement will remain. <br>
<br>
Where “features” are requested I suspect that most will want custom self-improving databases to be developed. For example, a certain form of scientific researcher may be desired, or strong enterprise decision intelligence may be requested. MyndSprout’s self-improving, code-generating nature allows it to rapidly proceed down different development paths. <br>
<br>
I am providing code generation services in C# and SQL.<br>
**10x coder == 1/10 cost.**<br>
Estimates: [TranscendAI.tech](https://TranscendAI.tech)<br>
<br>
![Footer Logo](MyndSprout.jpg)
*Copyright © 2025 Warren Harding — TranscendAI.tech — MyndSprout*
