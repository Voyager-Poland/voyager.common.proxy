#if NETFRAMEWORK
namespace Voyager.Common.Proxy.Server.Tests;

using Voyager.Common.Proxy.Server.Owin;

/// <summary>
/// Tests for <see cref="RouteMatcher"/> trailing slash normalization.
/// </summary>
public class RouteMatcherTests
{
    #region Static route (no parameters)

    [Fact]
    public void TryMatch_StaticRoute_WithoutTrailingSlash_Matches()
    {
        var matcher = new RouteMatcher("/api/Vip/Contact");

        var result = matcher.TryMatch("/api/Vip/Contact", out _);

        Assert.True(result);
    }

    [Fact]
    public void TryMatch_StaticRoute_WithTrailingSlash_Matches()
    {
        var matcher = new RouteMatcher("/api/Vip/Contact");

        var result = matcher.TryMatch("/api/Vip/Contact/", out _);

        Assert.True(result);
    }

    [Fact]
    public void TryMatch_StaticRoute_WithSuffix_DoesNotMatch()
    {
        var matcher = new RouteMatcher("/api/Vip/Contact");

        var result = matcher.TryMatch("/api/Vip/Contactx", out _);

        Assert.False(result);
    }

    [Fact]
    public void TryMatch_StaticRoute_WithDoubleTrailingSlash_DoesNotMatch()
    {
        var matcher = new RouteMatcher("/api/Vip/Contact");

        var result = matcher.TryMatch("/api/Vip/Contact//", out _);

        Assert.False(result);
    }

    [Fact]
    public void TryMatch_StaticRoute_WithExtraSegment_DoesNotMatch()
    {
        var matcher = new RouteMatcher("/api/Vip/Contact");

        var result = matcher.TryMatch("/api/Vip/Contact/extra", out _);

        Assert.False(result);
    }

    #endregion

    #region Parameterized route

    [Fact]
    public void TryMatch_ParameterizedRoute_WithoutTrailingSlash_Matches()
    {
        var matcher = new RouteMatcher("/api/users/{id}");

        var result = matcher.TryMatch("/api/users/123", out var routeValues);

        Assert.True(result);
        Assert.Equal("123", routeValues["id"]);
    }

    [Fact]
    public void TryMatch_ParameterizedRoute_WithTrailingSlash_Matches()
    {
        var matcher = new RouteMatcher("/api/users/{id}");

        var result = matcher.TryMatch("/api/users/123/", out var routeValues);

        Assert.True(result);
        Assert.Equal("123", routeValues["id"]);
    }

    [Fact]
    public void TryMatch_ParameterizedRoute_WithSuffix_DoesNotMatch()
    {
        var matcher = new RouteMatcher("/api/users/{id}/orders");

        var result = matcher.TryMatch("/api/users/123/ordersx", out _);

        Assert.False(result);
    }

    #endregion

    #region Root and edge cases

    [Fact]
    public void TryMatch_RootRoute_WithoutTrailingSlash_Matches()
    {
        var matcher = new RouteMatcher("/status");

        var result = matcher.TryMatch("/status", out _);

        Assert.True(result);
    }

    [Fact]
    public void TryMatch_RootRoute_WithTrailingSlash_Matches()
    {
        var matcher = new RouteMatcher("/status");

        var result = matcher.TryMatch("/status/", out _);

        Assert.True(result);
    }

    [Fact]
    public void TryMatch_CaseInsensitive_Matches()
    {
        var matcher = new RouteMatcher("/api/Vip/Contact");

        var result = matcher.TryMatch("/API/VIP/CONTACT/", out _);

        Assert.True(result);
    }

    #endregion
}
#endif
