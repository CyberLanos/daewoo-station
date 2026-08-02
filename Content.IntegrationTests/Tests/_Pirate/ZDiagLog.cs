// SPDX-License-Identifier: AGPL-3.0-or-later
// TEMPORARY diagnostic helper. Appends timestamped phase markers to a file so the
// EntityTest shard can be profiled under real (concurrent) CI conditions without
// enabling NUnit console output. Delete before merging.

using System.Diagnostics;
using System.IO;

namespace Content.IntegrationTests.Tests._Pirate;

public static class ZDiagLog
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Lock = new();
    private static readonly string Path =
        Environment.GetEnvironmentVariable("ZDIAG_LOG") ?? "zdiag.log";

    public static void Log(string message)
    {
        var line =
            $"[{Clock.Elapsed.TotalSeconds,9:F2}s] " +
            $"managed={GC.GetTotalMemory(false) / (1024 * 1024)}MB " +
            $"rss={Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)}MB " +
            $"gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)} gc2={GC.CollectionCount(2)} " +
            $"| {message}";

        lock (Lock)
        {
            File.AppendAllText(Path, line + Environment.NewLine);
        }
    }
}
