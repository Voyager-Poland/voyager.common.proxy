using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Proxy.Server.AspNetCore;

namespace Voyager.Common.Proxy.Server.IntegrationTests;

public class PermissionCheckingIntegrationTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #region Permission Checker Tests

    [Fact]
    public async Task PermissionChecker_WhenGranted_AllowsAccess()
    {
        // Arrange
        using var host = CreateHostWithPermissionChecker(_ => PermissionResult.Granted());
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PermissionChecker_WhenDenied_ReturnsForbidden()
    {
        // Arrange
        using var host = CreateHostWithPermissionChecker(_ => PermissionResult.Denied("Access denied"));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Access denied", content);
    }

    [Fact]
    public async Task PermissionChecker_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var host = CreateHostWithPermissionChecker(_ => PermissionResult.Unauthenticated());
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PermissionChecker_ReceivesCorrectContext()
    {
        // Arrange
        PermissionContext? capturedContext = null;

        using var host = CreateHostWithPermissionChecker(ctx =>
        {
            capturedContext = ctx;
            return PermissionResult.Granted();
        });
        var client = host.GetTestClient();

        // Act
        await client.GetAsync("/user-service/get-user?id=42");

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal(typeof(IUserService), capturedContext.ServiceType);
        Assert.Equal("GetUserAsync", capturedContext.Method.Name);
        Assert.True(capturedContext.Parameters.ContainsKey("id"));
        Assert.Equal(42, capturedContext.Parameters["id"]);
        Assert.IsAssignableFrom<HttpContext>(capturedContext.RawContext);
    }

    [Fact]
    public async Task PermissionChecker_CanCheckMethodName()
    {
        // Arrange - deny delete, allow everything else
        using var host = CreateHostWithPermissionChecker(ctx =>
            ctx.Method.Name == "DeleteUserAsync"
                ? PermissionResult.Denied("Delete not allowed")
                : PermissionResult.Granted());

        var client = host.GetTestClient();

        // Act & Assert - GET should work
        var getResponse = await client.GetAsync("/user-service/get-user?id=1");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // Act & Assert - DELETE should be denied
        var deleteResponse = await client.DeleteAsync("/user-service/delete-user?id=1");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task PermissionChecker_CanCheckParameters()
    {
        // Arrange - deny access to id > 100
        using var host = CreateHostWithPermissionChecker(ctx =>
        {
            if (ctx.Parameters.TryGetValue("id", out var idObj) && idObj is int id && id > 100)
                return PermissionResult.Denied("Cannot access id > 100");
            return PermissionResult.Granted();
        });

        var client = host.GetTestClient();

        // Act & Assert - id=1 should work
        var response1 = await client.GetAsync("/user-service/get-user?id=1");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Act & Assert - id=200 should be denied
        var response200 = await client.GetAsync("/user-service/get-user?id=200");
        Assert.Equal(HttpStatusCode.Forbidden, response200.StatusCode);
    }

    #endregion

    #region Context-Aware Factory Tests

    [Fact]
    public async Task ContextAwareFactory_ReceivesHttpContext()
    {
        // Arrange
        string? capturedMethod = null;
        string? capturedPath = null;

        using var host = CreateHostWithContextAwareFactory(httpContext =>
        {
            // Capture values during request (before context is disposed)
            capturedMethod = httpContext.Request.Method;
            capturedPath = httpContext.Request.Path;
            return new InMemoryUserService();
        });
        var client = host.GetTestClient();

        // Act
        await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.Equal("GET", capturedMethod);
        Assert.Equal("/user-service/get-user", capturedPath);
    }

    [Fact]
    public async Task ContextAwareFactory_CanAccessRequestServices()
    {
        // Arrange
        ICustomDependency? capturedDependency = null;

        using var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<ICustomDependency, CustomDependency>();
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapServiceProxy<IUserService>(options =>
                        {
                            options.ContextAwareFactory = httpContext =>
                            {
                                capturedDependency = httpContext.RequestServices
                                    .GetRequiredService<ICustomDependency>();
                                return new InMemoryUserService();
                            };
                        });
                    });
                });
            })
            .Build();

        await host.StartAsync();
        var client = host.GetTestClient();

        // Act
        await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.NotNull(capturedDependency);
        Assert.IsType<CustomDependency>(capturedDependency);
    }

    #endregion

    #region Combined Permission Checker and Factory Tests

    [Fact]
    public async Task PermissionCheckerAndFactory_BothWork()
    {
        // Arrange
        var factoryCalled = false;
        var permissionCheckerCalled = false;

        using var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapServiceProxy<IUserService>(options =>
                        {
                            options.ContextAwareFactory = _ =>
                            {
                                factoryCalled = true;
                                return new InMemoryUserService();
                            };
                            options.PermissionChecker = _ =>
                            {
                                permissionCheckerCalled = true;
                                return Task.FromResult(PermissionResult.Granted());
                            };
                        });
                    });
                });
            })
            .Build();

        await host.StartAsync();
        var client = host.GetTestClient();

        // Act
        await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.True(permissionCheckerCalled, "Permission checker should be called");
        Assert.True(factoryCalled, "Factory should be called after permission check");
    }

    [Fact]
    public async Task PermissionChecker_WhenDenied_FactoryNotCalled()
    {
        // Arrange
        var factoryCalled = false;

        using var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapServiceProxy<IUserService>(options =>
                        {
                            options.ContextAwareFactory = _ =>
                            {
                                factoryCalled = true;
                                return new InMemoryUserService();
                            };
                            options.PermissionChecker = _ =>
                                Task.FromResult(PermissionResult.Denied("Denied"));
                        });
                    });
                });
            })
            .Build();

        await host.StartAsync();
        var client = host.GetTestClient();

        // Act
        await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.False(factoryCalled, "Factory should not be called when permission denied");
    }

    #endregion

    #region Typed Permission Checker Tests

    [Fact]
    public async Task TypedPermissionChecker_Works()
    {
        // Arrange
        var checker = new TestPermissionChecker { ShouldGrant = true };

        using var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IUserService, InMemoryUserService>();
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapServiceProxy<IUserService>(options =>
                        {
                            options.PermissionCheckerInstance = checker;
                        });
                    });
                });
            })
            .Build();

        await host.StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(checker.WasCalled);
    }

    [Fact]
    public async Task TypedPermissionChecker_TakesPrecedenceOverCallback()
    {
        // Arrange
        var instanceChecker = new TestPermissionChecker { ShouldGrant = true };
        var callbackCalled = false;

        using var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IUserService, InMemoryUserService>();
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapServiceProxy<IUserService>(options =>
                        {
                            // Both are set - instance should take precedence
                            options.PermissionChecker = _ =>
                            {
                                callbackCalled = true;
                                return Task.FromResult(PermissionResult.Denied("Callback denied"));
                            };
                            options.PermissionCheckerInstance = instanceChecker;
                        });
                    });
                });
            })
            .Build();

        await host.StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Instance granted
        Assert.True(instanceChecker.WasCalled);
        Assert.False(callbackCalled);
    }

    #endregion

    #region Helper Methods

    private static IHost CreateHostWithPermissionChecker(
        Func<PermissionContext, PermissionResult> permissionChecker)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IUserService, InMemoryUserService>();
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapServiceProxy<IUserService>(options =>
                        {
                            options.PermissionChecker = ctx =>
                                Task.FromResult(permissionChecker(ctx));
                        });
                    });
                });
            })
            .Build();

        host.Start();
        return host;
    }

    private static IHost CreateHostWithContextAwareFactory(
        Func<HttpContext, IUserService> factory)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapServiceProxy<IUserService>(options =>
                        {
                            options.ContextAwareFactory = factory;
                        });
                    });
                });
            })
            .Build();

        host.Start();
        return host;
    }

    public void Dispose()
    {
        // Individual hosts are disposed in each test
    }

    #endregion

    #region Test Helpers

    private class TestPermissionChecker : IServicePermissionChecker<IUserService>
    {
        public bool ShouldGrant { get; set; } = true;
        public bool WasCalled { get; private set; }

        public Task<PermissionResult> CheckPermissionAsync(PermissionContext context)
        {
            WasCalled = true;
            return Task.FromResult(ShouldGrant
                ? PermissionResult.Granted()
                : PermissionResult.Denied("Test denial"));
        }
    }

    private interface ICustomDependency
    {
        string Name { get; }
    }

    private class CustomDependency : ICustomDependency
    {
        public string Name => "TestDependency";
    }

    #endregion
}
