/**
 * OcppTrace.cs — rolling-file trace for OCPPServer
 *
 * Trace levels (set in appsettings.json → Trace:Level):
 *   0  ERR only  — errors and unhandled exceptions
 *   1  MSG+ERR   — all OCPP/RMQ messages in+out, plus errors          (default)
 *   2  DBG+MSG+ERR — method entry/exit, all messages, all errors
 *
 * Provider tokens (fixed width 4 chars in the log line):
 *   OCPP — OCPP 1.6J protocol messages and handler logic
 *   RMQ  — RabbitMQ publish/consume events
 *   WS   — WebSocket connection lifecycle (connect / disconnect)
 *   HTTP — REST API calls received by this server
 *
 * Log line format:
 *   dd.MM.yyyy HH:mm:ss LEVL PROV: message
 *   e.g.  22.05.2026 14:35:22 MSG  OCPP: [← CALL] u030: action=Heartbeat
 *
 * File rotation (all under Trace:Directory, default /app/traces):
 *   OCPP_Trace1.txt — current file (always written here)
 *   OCPP_Trace2.txt — previous
 *   OCPP_Trace3.txt — oldest
 *
 *   When OCPP_Trace1.txt reaches Trace:MaxFileMB (default 10 MB):
 *     1. Delete  OCPP_Trace3.txt (if exists)
 *     2. Rename  OCPP_Trace2.txt → OCPP_Trace3.txt
 *     3. Rename  OCPP_Trace1.txt → OCPP_Trace2.txt
 *     4. New writes go to a fresh OCPP_Trace1.txt
 *
 * Thread safety: all file I/O is protected by _fileLock.
 * Console.WriteLine is called outside the lock (safe, concurrent-friendly).
 *
 * Download:
 *   GET /api/admin/traces             — list available files
 *   GET /api/admin/traces/{filename}  — download a file (requires ?key=<TraceKey>)
 */

namespace OCPPServer;

public static class OcppTrace
{
    // ── Configuration ──────────────────────────────────────────────────────────

    private static int    _level    = 1;
    private static string _dir      = "/app/traces";
    private static long   _maxBytes = 10L * 1024 * 1024; // 10 MB

    // ── File names (never change — download endpoint uses these as an allow-list) ──

    public const string File1 = "OCPP_Trace1.txt";
    public const string File2 = "OCPP_Trace2.txt";
    public const string File3 = "OCPP_Trace3.txt";

    private static readonly object _fileLock = new();

    // ── Startup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called once at startup (Program.cs). Creates the trace directory if needed.
    /// </summary>
    public static void Configure(string directory, int level, int maxFileMb = 10)
    {
        _level    = Math.Clamp(level, 0, 2);
        _dir      = directory;
        _maxBytes = (long)maxFileMb * 1024 * 1024;
        Directory.CreateDirectory(directory);
    }

    // ── Write methods ──────────────────────────────────────────────────────────

    /// <summary>
    /// Errors and unhandled exceptions. Always written regardless of trace level.
    /// </summary>
    public static void Error(string provider, string message)
        => Write("ERR ", provider, message);

    /// <summary>
    /// Incoming and outgoing OCPP / RMQ messages. Written at level ≥ 1.
    /// </summary>
    public static void Msg(string provider, string message)
    {
        if (_level >= 1) Write("MSG ", provider, message);
    }

    /// <summary>
    /// Method entry / detail tracing. Written at level 2 only.
    /// </summary>
    public static void Dbg(string provider, string message)
    {
        if (_level >= 2) Write("DBG ", provider, message);
    }

    // ── File helpers (used by download endpoint) ───────────────────────────────

    /// <summary>
    /// Returns metadata for each existing trace file (name + size in bytes).
    /// </summary>
    public static IEnumerable<(string Name, long Bytes)> GetFileList()
    {
        foreach (var name in new[] { File1, File2, File3 })
        {
            var path = Path.Combine(_dir, name);
            if (File.Exists(path))
                yield return (name, new FileInfo(path).Length);
        }
    }

    /// <summary>
    /// Returns the full path for a trace file, or null if the name is not one
    /// of the three known files (prevents path-traversal attacks).
    /// </summary>
    public static string? GetFilePath(string filename)
    {
        if (filename != File1 && filename != File2 && filename != File3) return null;
        var path = Path.Combine(_dir, filename);
        return File.Exists(path) ? path : null;
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    private static void Write(string level, string provider, string message)
    {
        // Format: dd.MM.yyyy HH:mm:ss LEVL PROV: message
        var line = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} {level}{provider,-4}: {message}";

        // Console output is not under the lock — Console is thread-safe internally.
        Console.WriteLine(line);

        lock (_fileLock)
        {
            RotateIfNeeded();
            File.AppendAllText(Path.Combine(_dir, File1), line + Environment.NewLine);
        }
    }

    /// <summary>
    /// Checks whether OCPP_Trace1.txt has reached the size limit and rotates if so.
    /// Must be called inside _fileLock.
    /// </summary>
    private static void RotateIfNeeded()
    {
        var p1 = Path.Combine(_dir, File1);
        if (!File.Exists(p1)) return;
        if (new FileInfo(p1).Length < _maxBytes) return;

        var p2 = Path.Combine(_dir, File2);
        var p3 = Path.Combine(_dir, File3);

        // Step 1: discard oldest
        if (File.Exists(p3)) File.Delete(p3);

        // Step 2: shift 2 → 3
        if (File.Exists(p2)) File.Move(p2, p3);

        // Step 3: shift 1 → 2  (next AppendAllText recreates Trace1)
        File.Move(p1, p2);
    }
}
