namespace OCPPServer;

/// <summary>
/// Thin wrapper around Console.WriteLine that prepends a UTC timestamp to every line.
/// All OCPP trace output should go through this so log correlation is possible.
/// </summary>
public static class OcppLog
{
    public static void Write(string message) =>
        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
}
