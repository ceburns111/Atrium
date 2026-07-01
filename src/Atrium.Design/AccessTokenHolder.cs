namespace Atrium.Design;

/// <summary>
/// Per-circuit holder for the signed-in user's access token. The shell populates it from the cascading
/// authentication state (which only a component may read); <see cref="BearerTokenHandler"/> reads the
/// plain string, since an HTTP message handler can't touch the AuthenticationStateProvider directly.
/// </summary>
public sealed class AccessTokenHolder
{
    public string? AccessToken { get; set; }
}
