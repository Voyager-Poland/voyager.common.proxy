using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Voyager.Common.Proxy.Server.AspNetCore;

namespace Voyager.Common.Proxy.Server.IntegrationTests;

/// <summary>
/// Test fixture that creates a test server with service proxy endpoints.
/// </summary>
public class ServerTestFixture : IDisposable
{
    private readonly IHost _host;

    public HttpClient Client { get; }

    public ServerTestFixture()
    {
        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    // Register test services as singletons to preserve state during tests
                    services.AddSingleton<IUserService, InMemoryUserService>();
                    services.AddSingleton<IOrderService, InMemoryOrderService>();
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapServiceProxy<IUserService>();
                        endpoints.MapServiceProxy<IOrderService>();
                    });
                });
            })
            .Build();

        _host.Start();
        Client = _host.GetTestClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();
    }
}

/// <summary>
/// Collection fixture for sharing test server across tests.
/// </summary>
[CollectionDefinition("Server")]
public class ServerCollection : ICollectionFixture<ServerTestFixture>
{
}
