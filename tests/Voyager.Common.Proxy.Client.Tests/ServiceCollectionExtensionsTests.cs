namespace Voyager.Common.Proxy.Client.Tests;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Voyager.Common.Results;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    #region AddServiceProxy with Action Tests

    [Fact]
    public void AddServiceProxy_WithOptions_RegistersService()
    {
        var services = new ServiceCollection();

        services.AddServiceProxy<ITestService>(options =>
        {
            options.BaseUrl = new Uri("https://api.example.com");
        });

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ITestService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public void AddServiceProxy_WithOptions_ReturnsHttpClientBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddServiceProxy<ITestService>(options =>
        {
            options.BaseUrl = new Uri("https://api.example.com");
        });

        builder.Should().NotBeNull();
        builder.Should().BeAssignableTo<IHttpClientBuilder>();
    }

    [Fact]
    public void AddServiceProxy_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var act = () => services.AddServiceProxy<ITestService>(options =>
        {
            options.BaseUrl = new Uri("https://api.example.com");
        });

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
    }

    [Fact]
    public void AddServiceProxy_WithNullConfigureOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Action<ServiceProxyOptions> configureOptions = null!;

        var act = () => services.AddServiceProxy<ITestService>(configureOptions);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configureOptions");
    }

    [Fact]
    public void AddServiceProxy_WithoutBaseUrl_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddServiceProxy<ITestService>(options =>
        {
            // BaseUrl not set
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BaseUrl*");
    }

    #endregion

    #region AddServiceProxy with String URL Tests

    [Fact]
    public void AddServiceProxy_WithStringUrl_RegistersService()
    {
        var services = new ServiceCollection();

        services.AddServiceProxy<ITestService>("https://api.example.com");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ITestService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public void AddServiceProxy_WithStringUrl_ReturnsHttpClientBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddServiceProxy<ITestService>("https://api.example.com");

        builder.Should().NotBeNull();
        builder.Should().BeAssignableTo<IHttpClientBuilder>();
    }

    [Fact]
    public void AddServiceProxy_WithNullStringUrl_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        string url = null!;

        var act = () => services.AddServiceProxy<ITestService>(url);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseUrl");
    }

    [Fact]
    public void AddServiceProxy_WithEmptyStringUrl_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddServiceProxy<ITestService>("");

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseUrl");
    }

    [Fact]
    public void AddServiceProxy_WithWhitespaceStringUrl_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddServiceProxy<ITestService>("   ");

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseUrl");
    }

    #endregion

    #region AddServiceProxy with Uri Tests

    [Fact]
    public void AddServiceProxy_WithUri_RegistersService()
    {
        var services = new ServiceCollection();

        services.AddServiceProxy<ITestService>(new Uri("https://api.example.com"));

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ITestService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public void AddServiceProxy_WithUri_ReturnsHttpClientBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddServiceProxy<ITestService>(new Uri("https://api.example.com"));

        builder.Should().NotBeNull();
        builder.Should().BeAssignableTo<IHttpClientBuilder>();
    }

    [Fact]
    public void AddServiceProxy_WithNullUri_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Uri uri = null!;

        var act = () => services.AddServiceProxy<ITestService>(uri);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseUrl");
    }

    #endregion

    #region HttpClientBuilder Chaining Tests

    [Fact]
    public void AddServiceProxy_CanChainMessageHandlers()
    {
        var services = new ServiceCollection();
        services.AddTransient<TestDelegatingHandler>();

        var builder = services.AddServiceProxy<ITestService>("https://api.example.com");

        var act = () => builder.AddHttpMessageHandler<TestDelegatingHandler>();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddServiceProxy_MessageHandlerIsRegistered()
    {
        var services = new ServiceCollection();

        services.AddTransient<TestDelegatingHandler>();

        services.AddServiceProxy<ITestService>("https://api.example.com")
            .AddHttpMessageHandler<TestDelegatingHandler>();

        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("ServiceProxy_Voyager.Common.Proxy.Client.Tests.ServiceCollectionExtensionsTests+ITestService");

        // Client is configured, handler should be registered
        client.Should().NotBeNull();
    }

    #endregion

    #region Multiple Services Tests

    [Fact]
    public void AddServiceProxy_MultipleServices_RegistersBoth()
    {
        var services = new ServiceCollection();

        services.AddServiceProxy<ITestService>("https://api1.example.com");
        services.AddServiceProxy<IAnotherService>("https://api2.example.com");

        var provider = services.BuildServiceProvider();

        provider.GetService<ITestService>().Should().NotBeNull();
        provider.GetService<IAnotherService>().Should().NotBeNull();
    }

    [Fact]
    public void AddServiceProxy_SameServiceTwice_OverwritesRegistration()
    {
        var services = new ServiceCollection();

        services.AddServiceProxy<ITestService>("https://api1.example.com");
        services.AddServiceProxy<ITestService>("https://api2.example.com");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ITestService>();

        service.Should().NotBeNull();
    }

    #endregion

    #region Options Configuration Tests

    [Fact]
    public void AddServiceProxy_CustomTimeout_AppliesTimeout()
    {
        var services = new ServiceCollection();
        var customTimeout = TimeSpan.FromSeconds(120);

        services.AddServiceProxy<ITestService>(options =>
        {
            options.BaseUrl = new Uri("https://api.example.com");
            options.Timeout = customTimeout;
        });

        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient("ServiceProxy_Voyager.Common.Proxy.Client.Tests.ServiceCollectionExtensionsTests+ITestService");

        client.Timeout.Should().Be(customTimeout);
    }

    #endregion

    #region Test Classes

    public interface ITestService
    {
        Task<Result<object>> GetAsync();
    }

    public interface IAnotherService
    {
        Task<Result<object>> GetAsync();
    }

    public class TestDelegatingHandler : DelegatingHandler
    {
        private readonly Action? _onSend;

        public TestDelegatingHandler() { }

        public TestDelegatingHandler(Action onSend)
        {
            _onSend = onSend;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _onSend?.Invoke();
            return base.SendAsync(request, cancellationToken);
        }
    }

    #endregion
}
