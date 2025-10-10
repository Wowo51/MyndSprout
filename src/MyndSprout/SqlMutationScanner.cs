//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MyndSprout.Security
{
    /// <summary>
    /// Pragmatic T-SQL mutation scanner for read-only enforcement.
    /// Sanitizes strings/comments/identifiers, then matches risky constructs.
    /// Treat this as a belt-and-suspenders check; still run least-privilege and/or
    /// wrap in a rollback-only transaction to be truly safe.
    /// </summary>
    public static class SqlMutationScanner
    {
        public sealed record Finding(string Category, string Pattern, int Index, string Excerpt);

        // === Public API ===

        /// <summary>True if SQL contains any potentially mutating construct.</summary>
        public static bool ContainsMutations(string sql) => Scan(sql).Count > 0;

        /// <summary>Scan and return detailed findings (category, pattern, index, excerpt).</summary>
        public static IReadOnlyList<Finding> Scan(string? sql)
        {
            var findings = new List<Finding>();
            if (string.IsNullOrWhiteSpace(sql)) return findings;

            string sanitized = Sanitize(sql, keepLength: true);
            string compact = CollapseWs(sanitized);

            foreach (var rule in Rules)
            {
                foreach (Match m in rule.Rx.Matches(compact))
                {
                    findings.Add(MakeFinding(rule.Category, rule.Rx.ToString(), m, compact));
                }
            }

            // De-dupe by (start,end,pattern), keep earliest category (stable)
            return findings
                .OrderBy(f => f.Index)
                .ThenBy(f => f.Category)
                .GroupBy(f => (f.Index, f.Pattern, f.Excerpt))
                .Select(g => g.First())
                .ToList();
        }

        // === Core rules ===
        private sealed class Rule
        {
            public string Category { get; init; } = "";
            public Regex Rx { get; init; } = null!;
        }

        // NOTE: We match on the compacted text (lowercase + collapsed whitespace around tokens).
        // Keep patterns lowercase and tolerant of whitespace.
        private static readonly Rule[] Rules = new[]
        {
            // --- DML ---
            R("DML",        @"\b(insert|update|delete|merge)\b"),
            R("DML",        @"\b\boutput\s+into\b"),                // capturing OUTPUT INTO (writes into table)
            R("DML",        @"\bbulk\s+insert\b"),
            R("DML",        @"\bopenrowset\s*\(\s*bulk\b"),

            // --- DDL (strong forms) ---
            R("DDL",        @"\bcreate\s+(table|view|function|procedure|proc|trigger|index|type|schema|database|role|user|assembly|sequence|synonym|external)\b"),
            R("DDL",        @"\balter\s+(table|view|function|procedure|proc|trigger|index|type|schema|database|role|user|assembly|sequence|synonym|external)\b"),
            R("DDL",        @"\bdrop\s+(table|view|function|procedure|proc|trigger|index|type|schema|database|role|user|assembly|sequence|synonym|external|login|server)\b"),
            R("DDL",        @"\btruncate\s+table\b"),
            R("DDL",        @"\bselect\b[\s\S]*?\binto\b"),         // SELECT ... INTO new_table (including temp)

            // --- Temp tables / table variables create ---
            R("DDL",        @"\bcreate\s+table\s+#"),               // CREATE TABLE #temp
            R("DDL",        @"\binto\s+#"),                         // SELECT ... INTO #temp
            R("DDL",        @"\bdeclare\s+@[a-z_][a-z0-9_]*\s+table\b"),

            // --- Exec / dynamic SQL / xprocs ---
            R("EXEC",       @"\bexec(?:ute)?\b"),
            R("EXEC",       @"\bsp_executesql\b"),
            R("EXEC",       @"\bxp_[a-z0-9_]+\b"),
            R("EXEC",       @"\bsp_rename\b"),

            // --- Security / permissions ---
            R("SECURITY",   @"\bgrant\b"),
            R("SECURITY",   @"\brevoke\b"),
            R("SECURITY",   @"\bdeny\b"),

            // --- Transactions / session state with side effects ---
            R("TXN",        @"\bbegin\s+tran(?:saction)?\b"),
            R("TXN",        @"\bcommit\b"),
            R("TXN",        @"\brollback\b"),

            // --- SET options with persistent or session-level impact ---
            R("SET",        @"\bset\s+identity_insert\b"),
            R("SET",        @"\bset\s+quoted_identifier\b"),
            R("SET",        @"\bset\s+ansi_nulls\b"),

            // --- Server / database level ---
            R("SERVER",     @"\bbackup\b"),
            R("SERVER",     @"\brestore\b"),
            R("SERVER",     @"\bdbcc\b"),
            R("SERVER",     @"\buse\s+[a-z_][a-z0-9_]*\b"),        // USE db (after sanitation it's safe)

            // --- Index maintenance ---
            R("INDEX",      @"\balter\s+index\b"),
            R("INDEX",      @"\brebuild\b"),                        // often appears with ALTER INDEX
            R("INDEX",      @"\breorganize\b"),

            // --- External / linked server ---
            R("EXT",        @"\bcreate\s+external\s+(data\s+source|file\s+format|table|library)\b"),
            R("SERVER",     @"\bsp_addlinkedserver\b|\bsp_dropserver\b|\bsp_serveroption\b"),
        };

        private static Rule R(string cat, string pat) => new()
        {
            Category = cat,
            Rx = new Regex(pat, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)
        };

        // === Sanitizer ===
        // Removes: -- line comments, /* */ block comments, 'â€¦' and N'â€¦' strings, 0xâ€¦ binary,
        // bracketed identifiers [ â€¦ ] and quoted identifiers " â€¦ ".
        // Keeps original length with spaces so indices are meaningful.
        private static string Sanitize(string input, bool keepLength)
        {
            var s = input;
            var sb = new StringBuilder(s.Length);
            int i = 0;

            void Emit(int count, char c = ' ')
            {
                if (keepLength) sb.Append(c, count);
            }

            while (i < s.Length)
            {
                char c = s[i];

                // -- line comment
                if (c == '-' && i + 1 < s.Length && s[i + 1] == '-')
                {
                    int start = i;
                    i += 2;
                    while (i < s.Length && s[i] != '\n' && s[i] != '\r') i++;
                    Emit(i - start);
                    continue;
                }

                // /* block comment */
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int start = i;
                    i += 2;
                    while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                    i = Math.Min(i + 2, s.Length);
                    Emit(i - start);
                    continue;
                }

                // N'â€¦' or 'â€¦' string literal (handles doubled single quotes)
                if (c == 'N' || c == 'n')
                {
                    if (i + 1 < s.Length && s[i + 1] == '\'')
                    {
                        int start = i;
                        i += 2;
                        while (i < s.Length)
                        {
                            if (s[i] == '\'')
                            {
                                if (i + 1 < s.Length && s[i + 1] == '\'') { i += 2; continue; }
                                i++; break;
                            }
                            i++;
                        }
                        Emit(i - start);
                        continue;
                    }
                }
                if (c == '\'')
                {
                    int start = i;
                    i++;
                    while (i < s.Length)
                    {
                        if (s[i] == '\'')
                        {
                            if (i + 1 < s.Length && s[i + 1] == '\'') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    Emit(i - start);
                    continue;
                }

                // 0xâ€¦ binary literal (hex run)
                if (c == '0' && i + 1 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X'))
                {
                    int start = i;
                    i += 2;
                    while (i < s.Length && IsHex(s[i])) i++;
                    Emit(i - start);
                    continue;
                }

                // [bracketed identifier]
                if (c == '[')
                {
                    int start = i;
                    i++;
                    while (i < s.Length && s[i] != ']') i++;
                    i = Math.Min(i + 1, s.Length);
                    Emit(i - start);
                    continue;
                }

                // "quoted identifier" (when QUOTED_IDENTIFIER ON)
                if (c == '"')
                {
                    int start = i;
                    i++;
                    while (i < s.Length)
                    {
                        if (s[i] == '"') { i++; break; }
                        i++;
                    }
                    Emit(i - start);
                    continue;
                }

                // Otherwise, copy 1 char (or space if keeping length)
                if (keepLength) sb.Append(s[i] == '\t' ? ' ' : s[i]);
                i++;
            }

            var outText = keepLength ? sb.ToString() : sb.ToString();
            return outText;
        }

        private static bool IsHex(char c)
            => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        // collapse whitespace and lowercase for robust multi-token matching
        private static string CollapseWs(string s)
        {
            var t = new StringBuilder(s.Length);
            bool inWs = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c))
                {
                    if (!inWs) { t.Append(' '); inWs = true; }
                }
                else
                {
                    t.Append(char.ToLowerInvariant(c));
                    inWs = false;
                }
            }
            return t.ToString();
        }

        private static Finding MakeFinding(string cat, string pattern, Match m, string text)
        {
            const int context = 40;
            int idx = m.Index;
            int start = Math.Max(0, idx - context);
            int end = Math.Min(text.Length, idx + m.Length + context);
            string excerpt = text.Substring(start, end - start).Trim();
            return new Finding(cat, pattern, idx, excerpt);
        }
    }
}

