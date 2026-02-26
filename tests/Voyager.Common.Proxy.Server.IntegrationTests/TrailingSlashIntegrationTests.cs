using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Voyager.Common.Proxy.Server.IntegrationTests;

/// <summary>
/// Tests that endpoints respond correctly to URLs with and without trailing slash.
/// Regression tests for ADR-017: Trailing Slash Normalization.
/// </summary>
[Collection("Server")]
public class TrailingSlashIntegrationTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public TrailingSlashIntegrationTests(ServerTestFixture fixture)
    {
        _client = fixture.Client;
    }

    #region Convention-based routes (no ServiceRoute attribute)

    [Fact]
    public async Task Get_ConventionRoute_WithoutTrailingSlash_ReturnsOk()
    {
        var response = await _client.GetAsync("/user-service/get-user?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ConventionRoute_WithTrailingSlash_ReturnsOk()
    {
        var response = await _client.GetAsync("/user-service/get-user/?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ConventionRoute_WithTrailingSlash_ReturnsOk()
    {
        var request = new CreateUserRequest("TrailingSlashUser", "slash@example.com");

        var response = await _client.PostAsJsonAsync("/user-service/create-user/", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<User>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal("TrailingSlashUser", user.Name);
    }

    #endregion

    #region ServiceRoute attribute routes

    [Fact]
    public async Task Get_ServiceRoute_WithoutTrailingSlash_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/orders/get-order?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ServiceRoute_WithTrailingSlash_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/orders/get-order/?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Parameterized routes

    [Fact]
    public async Task Get_ParameterizedRoute_WithoutTrailingSlash_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/orders/user/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ParameterizedRoute_WithTrailingSlash_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/orders/user/1/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region NoPrefix routes

    [Fact]
    public async Task Post_NoPrefixRoute_WithoutTrailingSlash_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/sale/callback", "test-data", JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_NoPrefixRoute_WithTrailingSlash_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/sale/callback/", "test-data", JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion
}
