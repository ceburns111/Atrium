namespace Atrium.Design;

/// <summary>
/// Per-circuit holder for the signed-in user's access token. The shell populates it from the cascading
/// authentication state (which only a component may read); the module's typed HTTP clients read the
/// plain string and attach it as a bearer header themselves. A <c>DelegatingHandler</c> can't be used
/// here — <c>IHttpClientFactory</c> resolves handlers in a separate scope, so the holder would be empty.
/// </summary>
public sealed class AccessTokenHolder
{
    public string? AccessToken { get; set; }
}
