namespace EVHomeAPI.Services;

/// <summary>
/// Charge point identities. EVOCPP lower-cases the id from the WebSocket URL (/ws/{id}) and keys
/// its connections by it, so events arrive lower-case and commands must be lower-case too.
/// EVHomeAPI stores and compares ids in the same canonical form.
///
/// '#' is allowed: chargers with several connectors may connect one socket per connector,
/// e.g. Wallbox Supernova with base id U005 → "U005#1", "U005#2" (in a URL: U005%231).
/// </summary>
public static class OcppIds
{
    public const string Pattern = "^[A-Za-z0-9_.#-]+$";

    public static string Normalize(string ocppId) => ocppId.Trim().ToLowerInvariant();
}
