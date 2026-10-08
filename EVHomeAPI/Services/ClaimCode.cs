using System.Security.Cryptography;

namespace EVHomeAPI.Services;

/// <summary>
/// One-time claim codes for pairing a station to its owner.
/// Format: XXXX-XXXX from an alphabet without look-alike characters (no 0/O, 1/I/L),
/// ~40 bits of entropy — enough with rate limiting on the claim endpoint.
/// </summary>
public static class ClaimCode
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static string Generate()
    {
        Span<char> chars = stackalloc char[9];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = i == 4 ? '-' : Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    /// <summary>Upper-case, strip spaces/dashes, so "abcd efgh" matches "ABCD-EFGH".</summary>
    public static string Normalize(string code) =>
        new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
