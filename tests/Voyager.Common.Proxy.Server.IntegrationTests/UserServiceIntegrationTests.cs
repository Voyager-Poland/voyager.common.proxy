using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Voyager.Common.Proxy.Server.IntegrationTests;

[Collection("Server")]
public class UserServiceIntegrationTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public UserServiceIntegrationTests(ServerTestFixture fixture)
    {
        _client = fixture.Client;
    }

    #region GET Tests

    [Fact]
    public async Task GetUser_ExistingId_ReturnsUser()
    {
        // Act - id is a query parameter
        var response = await _client.GetAsync("/user-service/get-user?id=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<User>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal(1, user.Id);
        Assert.Equal("Alice", user.Name);
    }

    [Fact]
    public async Task GetUser_NonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/user-service/get-user?id=999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_ReturnsAllUsers()
    {
        // Act
        var response = await _client.GetAsync("/user-service/get-all-users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<User[]>(JsonOptions);
        Assert.NotNull(users);
        Assert.True(users.Length >= 3);
    }

    [Fact]
    public async Task SearchUsers_WithNameFilter_ReturnsFilteredUsers()
    {
        // Act
        var response = await _client.GetAsync("/user-service/search-users?name=Ali");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<User[]>(JsonOptions);
        Assert.NotNull(users);
        Assert.All(users, u => Assert.Contains("Ali", u.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchUsers_WithLimitFilter_ReturnsLimitedUsers()
    {
        // Act
        var response = await _client.GetAsync("/user-service/search-users?limit=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<User[]>(JsonOptions);
        Assert.NotNull(users);
        Assert.True(users.Length <= 2);
    }

    [Fact]
    public async Task SearchUsers_WithBothFilters_ReturnsFilteredAndLimitedUsers()
    {
        // Act
        var response = await _client.GetAsync("/user-service/search-users?name=a&limit=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<User[]>(JsonOptions);
        Assert.NotNull(users);
        Assert.True(users.Length <= 1);
    }

    #endregion

    #region POST Tests

    [Fact]
    public async Task CreateUser_ValidRequest_ReturnsCreatedUser()
    {
        // Arrange
        var request = new CreateUserRequest("NewUser", "newuser@example.com");

        // Act
        var response = await _client.PostAsJsonAsync("/user-service/create-user", request, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<User>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal("NewUser", user.Name);
        Assert.Equal("newuser@example.com", user.Email);
        Assert.True(user.Id > 0);
    }

    [Fact]
    public async Task CreateUser_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserRequest("", "empty@example.com");

        // Act
        var response = await _client.PostAsJsonAsync("/user-service/create-user", request, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region PUT Tests

    [Fact]
    public async Task UpdateUser_ExistingId_ReturnsUpdatedUser()
    {
        // Arrange
        var request = new UpdateUserRequest("UpdatedName", "updated@example.com");

        // Act - id is a query parameter
        var response = await _client.PutAsJsonAsync("/user-service/update-user?id=2", request, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<User>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal(2, user.Id);
        Assert.Equal("UpdatedName", user.Name);
        Assert.Equal("updated@example.com", user.Email);
    }

    [Fact]
    public async Task UpdateUser_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateUserRequest("Name", "email@example.com");

        // Act
        var response = await _client.PutAsJsonAsync("/user-service/update-user?id=999", request, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region DELETE Tests

    [Fact]
    public async Task DeleteUser_ExistingId_ReturnsNoContent()
    {
        // First create a user to delete
        var createRequest = new CreateUserRequest("ToDelete", "delete@example.com");
        var createResponse = await _client.PostAsJsonAsync("/user-service/create-user", createRequest, JsonOptions);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<User>(JsonOptions);

        // Act - id is a query parameter
        var response = await _client.DeleteAsync($"/user-service/delete-user?id={createdUser!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify user is deleted
        var getResponse = await _client.GetAsync($"/user-service/get-user?id={createdUser.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_NonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/user-service/delete-user?id=9999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
