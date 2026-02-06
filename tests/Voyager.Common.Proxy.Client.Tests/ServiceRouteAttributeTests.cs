namespace Voyager.Common.Proxy.Client.Tests;

using FluentAssertions;
using Voyager.Common.Proxy.Abstractions;
using Xunit;

public class ServiceRouteAttributeTests
{
    [Fact]
    public void Constructor_WithValidPrefix_SetsPrefix()
    {
        var attr = new ServiceRouteAttribute("api/v2/users");

        attr.Prefix.Should().Be("api/v2/users");
    }

    [Fact]
    public void Constructor_WithLeadingAndTrailingSlashes_TrimsSlashes()
    {
        var attr = new ServiceRouteAttribute("/api/v2/users/");

        attr.Prefix.Should().Be("api/v2/users");
    }

    [Fact]
    public void Constructor_WithEmptyString_SetsEmptyPrefix()
    {
        var attr = new ServiceRouteAttribute("");

        attr.Prefix.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNoPrefixConstant_SetsEmptyPrefix()
    {
        var attr = new ServiceRouteAttribute(ServiceRouteAttribute.NoPrefix);

        attr.Prefix.Should().BeEmpty();
    }

    [Fact]
    public void NoPrefix_Constant_IsEmptyString()
    {
        ServiceRouteAttribute.NoPrefix.Should().Be("");
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException()
    {
        var act = () => new ServiceRouteAttribute(null!);

        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("prefix");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_WithWhitespaceOnly_ThrowsArgumentException(string prefix)
    {
        var act = () => new ServiceRouteAttribute(prefix);

        act.Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("prefix");
    }
}
