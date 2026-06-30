using Atrium.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atrium.Modules.Hello;

/// <summary>Throwaway module that exercises every part of the discovery contract.</summary>
public sealed class HelloModule : IModule
{
    public string Name => "Hello";

    public string Description =>
        "A throwaway module proving the contract: its page, service, and nav entry all wire up from a single project reference.";

    public string BasePath => "/hello";

    public IEnumerable<NavItem> NavItems => [new NavItem("Hello", "/hello")];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<HelloService>();
}
