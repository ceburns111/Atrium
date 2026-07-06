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

    private bool _sessionEnded;

    /// <summary>The signed-in user's raw access token (a JWT), or null when anonymous.</summary>
    public string? AccessToken
    {
        get => _accessToken;
        set
        {
            _accessToken = value;
            ExpiresAt = ReadExpiry(value);
            // A fresh, live token revives the session. Re-setting the SAME already-dead token — which
            // the shell does on every re-render via OnParametersSetAsync — must NOT clear a session we
            // have already marked ended, or the topbar would flip back to "signed in".
            if (!IsExpired)
            {
                _sessionEnded = false;
            }
        }
    }

    /// <summary>
    /// Raised when a dead session is first detected, so shell UI (the topbar account control) can
    /// re-render and stop presenting a signed-in menu the instant the session ends — staying consistent
    /// with the "session expired" banner even when expiry surfaces without a navigation.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// True once a dead session has been detected — an already-expired token short-circuited a request,
    /// or a downstream 401 came back. Sticky until a fresh live token replaces it. This is the single
    /// signal the shell reads to present a consistent signed-out state everywhere (see
    /// <see cref="EndSession"/>); <see cref="IsExpired"/> remains the time-derived fail-fast used by the
    /// request pipeline itself.
    /// </summary>
    public bool SessionEnded => _sessionEnded;

    /// <summary>
    /// Marks the session ended and notifies subscribers. Idempotent — the shell's error boundary calls
    /// this the moment it turns a <see cref="SessionExpiredException"/> into the re-login prompt, so the
    /// topbar and the banner flip together.
    /// </summary>
    public void EndSession()
    {
        if (_sessionEnded)
        {
            return;
        }
        _sessionEnded = true;
        Changed?.Invoke();
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
