using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EVHomeAPI.Push;

public enum PushResult { Sent, InvalidToken, Failed }

public interface IPushSender
{
    bool Enabled { get; }
    Task<PushResult> SendAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken ct = default);
}

/// <summary>Fcm:ServiceAccountJsonBase64 = base64 of the Firebase service-account JSON (secret).</summary>
public sealed class FcmOptions
{
    public const string Section = "Fcm";
    public string? ServiceAccountJsonBase64 { get; set; }
}

/// <summary>
/// Firebase Cloud Messaging HTTP v1. Authenticates with the service account (RS256-signed JWT →
/// OAuth2 access token, cached ~1 h). No Firebase SDK: two plain HTTPS calls.
/// Without credentials it is disabled and every send is a no-op (local dev, tests).
/// </summary>
public sealed class FcmSender : IPushSender
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly ILogger<FcmSender> _logger;
    private readonly ServiceAccount? _account;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string?  _accessToken;
    private DateTime _accessTokenExpires;

    public FcmSender(HttpClient http, FcmOptions options, TimeProvider clock, ILogger<FcmSender> logger)
    {
        _http   = http;
        _clock  = clock;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(options.ServiceAccountJsonBase64))
        {
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(options.ServiceAccountJsonBase64.Trim()));
                _account = JsonSerializer.Deserialize<ServiceAccount>(json);
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                logger.LogError("Fcm:ServiceAccountJsonBase64 is not valid base64 service-account JSON — push disabled");
            }
        }
        if (_account is null) logger.LogInformation("FCM credentials not configured — push notifications disabled");
    }

    public bool Enabled => _account is { ProjectId.Length: > 0, ClientEmail.Length: > 0, PrivateKey.Length: > 0 };

    public async Task<PushResult> SendAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        if (!Enabled) return PushResult.Failed;

        var message = new
        {
            message = new
            {
                token,
                notification = new { title, body },
                data,
                android = new { priority = "HIGH", notification = new { channel_id = "charging" } },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{_account!.ProjectId}/messages:send")
        {
            Content = JsonContent(message),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(ct));

        using var res = await _http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode) return PushResult.Sent;

        var error = await res.Content.ReadAsStringAsync(ct);
        // UNREGISTERED (404) / INVALID_ARGUMENT for a bad token → drop it.
        if (res.StatusCode == HttpStatusCode.NotFound || error.Contains("UNREGISTERED") ||
            (res.StatusCode == HttpStatusCode.BadRequest && error.Contains("registration token")))
            return PushResult.InvalidToken;

        _logger.LogWarning("FCM send failed: {Status} {Error}", (int)res.StatusCode, error.Length > 300 ? error[..300] : error);
        return PushResult.Failed;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (_accessToken is not null && now < _accessTokenExpires) return _accessToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && now < _accessTokenExpires) return _accessToken;

            var tokenUri  = _account!.TokenUri ?? "https://oauth2.googleapis.com/token";
            var assertion = CreateAssertion(_account, tokenUri, _clock.GetUtcNow());
            using var res = await _http.PostAsync(tokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"]  = assertion,
            }), ct);
            res.EnsureSuccessStatusCode();
            var tok = await res.Content.ReadFromJsonAsync<TokenResponse>(ct) ?? throw new InvalidOperationException("Empty token response");

            _accessToken        = tok.AccessToken;
            _accessTokenExpires = now.AddSeconds(Math.Max(60, tok.ExpiresIn - 300));   // refresh 5 min early
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>Google service-account JWT (RFC 7523): header.claims signed RS256 with the account's key.</summary>
    internal static string CreateAssertion(ServiceAccount account, string audience, DateTimeOffset now)
    {
        var header = new { alg = "RS256", typ = "JWT" };
        var claims = new
        {
            iss   = account.ClientEmail,
            scope = Scope,
            aud   = audience,
            iat   = now.ToUnixTimeSeconds(),
            exp   = now.AddHours(1).ToUnixTimeSeconds(),
        };
        var unsigned = $"{B64Url(JsonSerializer.SerializeToUtf8Bytes(header))}.{B64Url(JsonSerializer.SerializeToUtf8Bytes(claims))}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var sig = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{B64Url(sig)}";
    }

    private static string B64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static StringContent JsonContent(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");

    internal sealed record ServiceAccount(
        [property: JsonPropertyName("project_id")]   string ProjectId,
        [property: JsonPropertyName("client_email")] string ClientEmail,
        [property: JsonPropertyName("private_key")]  string PrivateKey,
        [property: JsonPropertyName("token_uri")]    string? TokenUri);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")]   int ExpiresIn);
}
