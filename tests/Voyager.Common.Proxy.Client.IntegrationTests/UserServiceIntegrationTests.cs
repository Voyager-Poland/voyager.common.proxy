namespace Voyager.Common.Proxy.Client.IntegrationTests;

using FluentAssertions;
using Voyager.Common.Proxy.Client.IntegrationTests.Contracts;
using Voyager.Common.Proxy.Client.IntegrationTests.TestServer;
using Voyager.Common.Results;
using Xunit;

/// <summary>
/// Integration tests for convention-based routing (IUserService).
/// </summary>
public class UserServiceIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IUserService _userService;

    public UserServiceIntegrationTests()
    {
        TestServerSetup.ResetData();
        _httpClient = TestServerSetup.CreateTestClient();

        var options = new ServiceProxyOptions
        {
            BaseUrl = _httpClient.BaseAddress!
        };

        _userService = ServiceProxy<IUserService>.Create(_httpClient, options);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    #region GetUser Tests

    [Fact]
    public async Task GetUserAsync_ExistingUser_ReturnsSuccess()
    {
        var result = await _userService.GetUserAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(1);
        result.Value.Name.Should().Be("John Doe");
        result.Value.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task GetUserAsync_NonExistingUser_ReturnsNotFound()
    {
        var result = await _userService.GetUserAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Contain("999");
    }

    [Fact]
    public async Task GetUserAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();
        var result = await _userService.GetUserAsync(1, cts.Token);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(1);
    }

    #endregion

    #region ListUsers Tests

    [Fact]
    public async Task ListUsersAsync_NoFilter_ReturnsAllUsers()
    {
        var result = await _userService.ListUsersAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListUsersAsync_WithNameFilter_ReturnsFilteredUsers()
    {
        var result = await _userService.ListUsersAsync(nameFilter: "John");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Contain("John");
    }

    [Fact]
    public async Task ListUsersAsync_WithLimit_ReturnsLimitedUsers()
    {
        var result = await _userService.ListUsersAsync(limit: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListUsersAsync_WithFilterAndLimit_ReturnsCombinedResult()
    {
        var result = await _userService.ListUsersAsync(nameFilter: "o", limit: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
    }

    #endregion

    #region CreateUser Tests

    [Fact]
    public async Task CreateUserAsync_ValidRequest_ReturnsCreatedUser()
    {
        var request = new CreateUserRequest
        {
            Name = "Alice Brown",
            Email = "alice@example.com"
        };

        var result = await _userService.CreateUserAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().BeGreaterThan(0);
        result.Value.Name.Should().Be("Alice Brown");
        result.Value.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task CreateUserAsync_MissingName_ReturnsValidationError()
    {
        var request = new CreateUserRequest
        {
            Name = "",
            Email = "test@example.com"
        };

        var result = await _userService.CreateUserAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Name");
    }

    [Fact]
    public async Task CreateUserAsync_MissingEmail_ReturnsValidationError()
    {
        var request = new CreateUserRequest
        {
            Name = "Test User",
            Email = ""
        };

        var result = await _userService.CreateUserAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Email");
    }

    #endregion

    #region UpdateUser Tests

    [Fact]
    public async Task UpdateUserAsync_ExistingUser_ReturnsUpdatedUser()
    {
        var updatedUser = new User
        {
            Id = 1,
            Name = "John Updated",
            Email = "john.updated@example.com"
        };

        var result = await _userService.UpdateUserAsync(updatedUser);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(1);
        result.Value.Name.Should().Be("John Updated");
        result.Value.Email.Should().Be("john.updated@example.com");
    }

    [Fact]
    public async Task UpdateUserAsync_NonExistingUser_ReturnsNotFound()
    {
        var user = new User
        {
            Id = 999,
            Name = "Ghost",
            Email = "ghost@example.com"
        };

        var result = await _userService.UpdateUserAsync(user);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    #endregion

    #region DeleteUser Tests

    [Fact]
    public async Task DeleteUserAsync_ExistingUser_ReturnsSuccess()
    {
        var result = await _userService.DeleteUserAsync(1);

        result.IsSuccess.Should().BeTrue();

        // Verify user is actually deleted
        var getResult = await _userService.GetUserAsync(1);
        getResult.IsFailure.Should().BeTrue();
        getResult.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteUserAsync_NonExistingUser_ReturnsNotFound()
    {
        var result = await _userService.DeleteUserAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    #endregion

    #region Full CRUD Flow Tests

    [Fact]
    public async Task FullCrudFlow_CreateReadUpdateDelete_WorksCorrectly()
    {
        // Create
        var createRequest = new CreateUserRequest
        {
            Name = "CRUD Test User",
            Email = "crud@example.com"
        };
        var createResult = await _userService.CreateUserAsync(createRequest);
        createResult.IsSuccess.Should().BeTrue();
        var userId = createResult.Value!.Id;

        // Read
        var readResult = await _userService.GetUserAsync(userId);
        readResult.IsSuccess.Should().BeTrue();
        readResult.Value!.Name.Should().Be("CRUD Test User");

        // Update
        var updateUser = new User
        {
            Id = userId,
            Name = "CRUD Test User Updated",
            Email = "crud.updated@example.com"
        };
        var updateResult = await _userService.UpdateUserAsync(updateUser);
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.Name.Should().Be("CRUD Test User Updated");

        // Verify update
        var verifyResult = await _userService.GetUserAsync(userId);
        verifyResult.IsSuccess.Should().BeTrue();
        verifyResult.Value!.Name.Should().Be("CRUD Test User Updated");

        // Delete
        var deleteResult = await _userService.DeleteUserAsync(userId);
        deleteResult.IsSuccess.Should().BeTrue();

        // Verify delete
        var finalResult = await _userService.GetUserAsync(userId);
        finalResult.IsFailure.Should().BeTrue();
        finalResult.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    #endregion
}
