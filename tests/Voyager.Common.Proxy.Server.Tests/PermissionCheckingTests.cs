using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Server.Abstractions;

namespace Voyager.Common.Proxy.Server.Tests;

public class PermissionCheckingTests
{
    #region PermissionResult Tests

    [Fact]
    public void PermissionResult_Granted_ReturnsIsGrantedTrue()
    {
        var result = PermissionResult.Granted();

        Assert.True(result.IsGranted);
        Assert.Null(result.DenialReason);
        Assert.False(result.IsAuthenticationFailure);
    }

    [Fact]
    public void PermissionResult_Denied_ReturnsIsGrantedFalse()
    {
        var result = PermissionResult.Denied("Access denied");

        Assert.False(result.IsGranted);
        Assert.Equal("Access denied", result.DenialReason);
        Assert.False(result.IsAuthenticationFailure);
    }

    [Fact]
    public void PermissionResult_Denied_WithoutReason_UsesDefaultMessage()
    {
        var result = PermissionResult.Denied(null!);

        Assert.False(result.IsGranted);
        Assert.Equal("Permission denied", result.DenialReason);
    }

    [Fact]
    public void PermissionResult_Unauthenticated_ReturnsIsAuthenticationFailureTrue()
    {
        var result = PermissionResult.Unauthenticated();

        Assert.False(result.IsGranted);
        Assert.True(result.IsAuthenticationFailure);
        Assert.Equal("Authentication required", result.DenialReason);
    }

    [Fact]
    public void PermissionResult_Unauthenticated_WithCustomReason()
    {
        var result = PermissionResult.Unauthenticated("Token expired");

        Assert.False(result.IsGranted);
        Assert.True(result.IsAuthenticationFailure);
        Assert.Equal("Token expired", result.DenialReason);
    }

    [Fact]
    public void PermissionResult_ImplicitBoolTrue_ReturnsGranted()
    {
        PermissionResult result = true;

        Assert.True(result.IsGranted);
    }

    [Fact]
    public void PermissionResult_ImplicitBoolFalse_ReturnsDenied()
    {
        PermissionResult result = false;

        Assert.False(result.IsGranted);
        Assert.Equal("Permission denied", result.DenialReason);
    }

    #endregion

    #region PermissionContext Tests

    [Fact]
    public void PermissionContext_Constructor_SetsAllProperties()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser")
        }, "test"));

        var serviceType = typeof(ITestService);
        var method = serviceType.GetMethod(nameof(ITestService.TestMethodAsync))!;
        var endpoint = CreateTestEndpoint(serviceType, method);
        var parameters = new Dictionary<string, object?> { ["id"] = 42 };
        var rawContext = new object();

        var context = new PermissionContext(
            user,
            serviceType,
            method,
            endpoint,
            parameters,
            rawContext);

        Assert.Same(user, context.User);
        Assert.Same(serviceType, context.ServiceType);
        Assert.Same(method, context.Method);
        Assert.Same(endpoint, context.Endpoint);
        Assert.Same(parameters, context.Parameters);
        Assert.Same(rawContext, context.RawContext);
    }

    [Fact]
    public void PermissionContext_Constructor_AcceptsNullUser()
    {
        var serviceType = typeof(ITestService);
        var method = serviceType.GetMethod(nameof(ITestService.TestMethodAsync))!;
        var endpoint = CreateTestEndpoint(serviceType, method);
        var parameters = new Dictionary<string, object?>();
        var rawContext = new object();

        var context = new PermissionContext(
            null,
            serviceType,
            method,
            endpoint,
            parameters,
            rawContext);

        Assert.Null(context.User);
    }

    [Fact]
    public void PermissionContext_Constructor_ThrowsOnNullServiceType()
    {
        var method = typeof(ITestService).GetMethod(nameof(ITestService.TestMethodAsync))!;
        var endpoint = CreateTestEndpoint(typeof(ITestService), method);

        Assert.Throws<ArgumentNullException>(() => new PermissionContext(
            null, null!, method, endpoint, new Dictionary<string, object?>(), new object()));
    }

    [Fact]
    public void PermissionContext_Constructor_ThrowsOnNullMethod()
    {
        var serviceType = typeof(ITestService);
        var method = serviceType.GetMethod(nameof(ITestService.TestMethodAsync))!;
        var endpoint = CreateTestEndpoint(serviceType, method);

        Assert.Throws<ArgumentNullException>(() => new PermissionContext(
            null, serviceType, null!, endpoint, new Dictionary<string, object?>(), new object()));
    }

    #endregion

    #region ServiceProxyOptionsBase Tests

    [Fact]
    public void GetEffectivePermissionChecker_WithNoChecker_ReturnsNull()
    {
        var options = new TestServiceProxyOptions();

        var checker = options.GetEffectivePermissionChecker();

        Assert.Null(checker);
    }

    [Fact]
    public void GetEffectivePermissionChecker_WithCallback_ReturnsCallback()
    {
        var options = new TestServiceProxyOptions();
        Func<PermissionContext, Task<PermissionResult>> callback =
            _ => Task.FromResult(PermissionResult.Granted());

        options.PermissionChecker = callback;

        var checker = options.GetEffectivePermissionChecker();

        Assert.Same(callback, checker);
    }

    [Fact]
    public void GetEffectivePermissionChecker_WithInstance_ReturnsInstanceMethod()
    {
        var options = new TestServiceProxyOptions();
        var instance = new TestPermissionChecker();

        options.PermissionCheckerInstance = instance;

        var checker = options.GetEffectivePermissionChecker();

        Assert.NotNull(checker);
    }

    [Fact]
    public void GetEffectivePermissionChecker_WithBoth_InstanceTakesPrecedence()
    {
        var options = new TestServiceProxyOptions();
        var callbackCalled = false;
        Func<PermissionContext, Task<PermissionResult>> callback = _ =>
        {
            callbackCalled = true;
            return Task.FromResult(PermissionResult.Granted());
        };

        var instance = new TestPermissionChecker();

        options.PermissionChecker = callback;
        options.PermissionCheckerInstance = instance;

        var checker = options.GetEffectivePermissionChecker();

        // Should return the instance's method, not the callback
        Assert.NotSame(callback, checker);
        Assert.False(callbackCalled);
    }

    [Fact]
    public async Task GetEffectivePermissionChecker_InstanceMethod_Works()
    {
        var options = new TestServiceProxyOptions();
        var instance = new TestPermissionChecker { ShouldGrant = true };
        options.PermissionCheckerInstance = instance;

        var checker = options.GetEffectivePermissionChecker();
        var context = CreateMinimalPermissionContext();

        var result = await checker!(context);

        Assert.True(result.IsGranted);
        Assert.True(instance.WasCalled);
    }

    [Fact]
    public async Task GetEffectivePermissionChecker_InstanceMethod_CanDeny()
    {
        var options = new TestServiceProxyOptions();
        var instance = new TestPermissionChecker { ShouldGrant = false };
        options.PermissionCheckerInstance = instance;

        var checker = options.GetEffectivePermissionChecker();
        var context = CreateMinimalPermissionContext();

        var result = await checker!(context);

        Assert.False(result.IsGranted);
        Assert.Equal("Test denial", result.DenialReason);
    }

    #endregion

    #region Test Helpers

    public interface ITestService
    {
        Task<string> TestMethodAsync(int id);
    }

    private class TestServiceProxyOptions : ServiceProxyOptionsBase<ITestService>
    {
    }

    private class TestPermissionChecker : IServicePermissionChecker<ITestService>
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

    private static EndpointDescriptor CreateTestEndpoint(Type serviceType, MethodInfo method)
    {
        return new EndpointDescriptor(
            serviceType,
            method,
            "GET",
            "/test/{id}",
            Array.Empty<ParameterDescriptor>(),
            typeof(string),
            typeof(string));
    }

    private PermissionContext CreateMinimalPermissionContext()
    {
        var serviceType = typeof(ITestService);
        var method = serviceType.GetMethod(nameof(ITestService.TestMethodAsync))!;
        var endpoint = CreateTestEndpoint(serviceType, method);

        return new PermissionContext(
            null,
            serviceType,
            method,
            endpoint,
            new Dictionary<string, object?>(),
            new object());
    }

    #endregion
}
