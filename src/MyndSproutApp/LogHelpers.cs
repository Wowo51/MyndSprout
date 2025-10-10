//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;

namespace MyndSproutApp
{
    internal static class LogHelpers
    {
        // Splits large payloads to multiple log lines to avoid UI hiccups / clipboard limits.
        public static void LogBlock(Action<string> log, string header, string payload, int chunkSize = 4000)
        {
            if (log == null) return;
            header ??= "";
            payload ??= "";

            log($"[{header}] BEGIN (length={payload.Length:N0})");
            if (payload.Length == 0)
            {
                log($"[{header}] (empty)");
                log($"[{header}] END");
                return;
            }

            log(payload);

            log($"[{header}] END");
        }
    }
}
