using System.Text.Json;

namespace Atrium.Design;

/// <summary>
/// Per-circuit holder for the signed-in user's access token. The shell populates it from the cascading
/// authentication state (which only a component may read); the module's typed HTTP clients read the
/// plain string and attach it as a bearer header themselves. A <c>DelegatingHandler</c> can't be used
/// here — <c>IHttpClientFactory</c> resolves handlers in a separate scope, so the holder would be empty.
/// </summary>
public sealed class AccessTokenHolder
{
    private string? _accessToken;

    /// <summary>The signed-in user's raw access token (a JWT), or null when anonymous.</summary>
    public string? AccessToken
    {
        get => _accessToken;
        set
        {
            _accessToken = value;
            ExpiresAt = ReadExpiry(value);
        }
    }

    /// <summary>
    /// The access token's expiry, read from its own <c>exp</c> claim when the token was set; null when
    /// there is no token or its expiry can't be parsed.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// True when a token is present but its expiry has already passed — a dead session. Anonymous
    /// callers (no token) and tokens with no parseable expiry read as not-expired, so genuine anonymous
    /// browsing and any parse hiccup fall back to the reactive 401 path (ADR-0008) rather than locking
    /// the user out. Read by the shared <c>Authorize</c> step so a dead session fails fast on every
    /// request — even one bound for an anonymous endpoint that would otherwise answer 200 and hide it.
    /// </summary>
    public bool IsExpired =>
        !string.IsNullOrEmpty(_accessToken)
        && ExpiresAt is { } expiry
        && expiry <= DateTimeOffset.UtcNow;

    // Reads `exp` (seconds since the Unix epoch) from a JWT's payload segment WITHOUT validating the
    // signature — the resource servers still do that; here we only need the client-visible expiry to
    // fail a dead session fast. Any malformed or non-JWT input yields null (fail open).
    private static DateTimeOffset? ReadExpiry(string? jwt)
    {
        if (string.IsNullOrEmpty(jwt))
        {
            return null;
        }

        var segments = jwt.Split('.');
        if (segments.Length < 2)
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(Base64UrlDecode(segments[1]));
            return
                payload.RootElement.TryGetProperty("exp", out var exp)
                && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };
        return Convert.FromBase64String(padded);
    }
}
