using System.Net;

namespace Atrium.Design;

/// <summary>
/// Signals that a downstream API answered <c>401 Unauthorized</c> — in this demo that means the access
/// token captured at login has expired (there is no refresh; see ADR-0004). The shell's
/// <c>SessionErrorBoundary</c> catches it and shows a "sign in again" prompt, rather than letting a raw
/// <see cref="HttpRequestException"/> tear the Blazor circuit down with the generic unhandled-error UI.
/// </summary>
public sealed class SessionExpiredException() : Exception("The signed-in session has expired.");

public static class HttpResponseSessionExtensions
{
    /// <summary>
    /// Throws <see cref="SessionExpiredException"/> when the response is a 401. Call it before
    /// <c>EnsureSuccessStatusCode()</c> so an expired token surfaces as a typed, recoverable signal
    /// instead of a generic failure.
    /// </summary>
    public static void ThrowIfSessionExpired(this HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new SessionExpiredException();
        }
    }
}
