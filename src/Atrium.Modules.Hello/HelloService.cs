namespace Atrium.Modules.Hello;

/// <summary>
/// A trivial service the module registers itself, so the page injecting it proves the host honored
/// <see cref="HelloModule.RegisterServices"/>.
/// </summary>
public sealed class HelloService
{
    public string Greeting { get; } = "Hello from a module the host discovered at startup 👋";
}
