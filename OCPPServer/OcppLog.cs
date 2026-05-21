namespace OCPPServer;

/// <summary>
/// Legacy thin wrapper — kept for backward compatibility.
/// All new code should call OcppTrace.Msg / OcppTrace.Error / OcppTrace.Dbg directly.
/// </summary>
[Obsolete("Use OcppTrace.Msg, OcppTrace.Error, or OcppTrace.Dbg instead.")]
public static class OcppLog
{
    public static void Write(string message) =>
        OcppTrace.Msg("OCPP", message);
}
