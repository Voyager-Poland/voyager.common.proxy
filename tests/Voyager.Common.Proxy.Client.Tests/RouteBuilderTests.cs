namespace Voyager.Common.Proxy.Client.Tests;

using System.Reflection;
using FluentAssertions;
using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Proxy.Client.Internal;
using Voyager.Common.Results;
using Xunit;

public class RouteBuilderTests
{
    #region GetHttpMethod Tests

    [Theory]
    [InlineData("GetUser", HttpMethod.Get)]
    [InlineData("GetUserAsync", HttpMethod.Get)]
    [InlineData("GetById", HttpMethod.Get)]
    [InlineData("FindUser", HttpMethod.Get)]
    [InlineData("FindAllUsers", HttpMethod.Get)]
    [InlineData("ListUsers", HttpMethod.Get)]
    [InlineData("ListAll", HttpMethod.Get)]
    public void GetHttpMethod_GetPrefixes_ReturnsGet(string methodName, HttpMethod expected)
    {
        var method = CreateMethod(methodName);

        var result = RouteBuilder.GetHttpMethod(method);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("CreateUser", HttpMethod.Post)]
    [InlineData("CreateUserAsync", HttpMethod.Post)]
    [InlineData("AddUser", HttpMethod.Post)]
    [InlineData("AddItem", HttpMethod.Post)]
    public void GetHttpMethod_PostPrefixes_ReturnsPost(string methodName, HttpMethod expected)
    {
        var method = CreateMethod(methodName);

        var result = RouteBuilder.GetHttpMethod(method);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("UpdateUser", HttpMethod.Put)]
    [InlineData("UpdateUserAsync", HttpMethod.Put)]
    [InlineData("UpdateById", HttpMethod.Put)]
    public void GetHttpMethod_PutPrefixes_ReturnsPut(string methodName, HttpMethod expected)
    {
        var method = CreateMethod(methodName);

        var result = RouteBuilder.GetHttpMethod(method);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("DeleteUser", HttpMethod.Delete)]
    [InlineData("DeleteUserAsync", HttpMethod.Delete)]
    [InlineData("RemoveUser", HttpMethod.Delete)]
    [InlineData("RemoveItem", HttpMethod.Delete)]
    public void GetHttpMethod_DeletePrefixes_ReturnsDelete(string methodName, HttpMethod expected)
    {
        var method = CreateMethod(methodName);

        var result = RouteBuilder.GetHttpMethod(method);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ProcessOrder", HttpMethod.Post)]
    [InlineData("ExecuteCommand", HttpMethod.Post)]
    [InlineData("DoSomething", HttpMethod.Post)]
    public void GetHttpMethod_UnknownPrefix_ReturnsPost(string methodName, HttpMethod expected)
    {
        var method = CreateMethod(methodName);

        var result = RouteBuilder.GetHttpMethod(method);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetHttpMethod_WithHttpGetAttribute_ReturnsGet()
    {
        var method = typeof(IServiceWithAttributes).GetMethod(nameof(IServiceWithAttributes.CustomMethod))!;

        var result = RouteBuilder.GetHttpMethod(method);

        result.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public void GetHttpMethod_WithHttpPostAttribute_ReturnsPost()
    {
        var method = typeof(IServiceWithAttributes).GetMethod(nameof(IServiceWithAttributes.PostMethod))!;

        var result = RouteBuilder.GetHttpMethod(method);

        result.Should().Be(HttpMethod.Post);
    }

    #endregion

    #region GetServicePrefix Tests

    [Fact]
    public void GetServicePrefix_InterfaceWithIPrefix_RemovesPrefixAndConvertsToKebabCase()
    {
        var result = RouteBuilder.GetServicePrefix(typeof(IUserService));

        result.Should().Be("user-service");
    }

    [Fact]
    public void GetServicePrefix_InterfaceWithoutIPrefix_ConvertsToKebabCase()
    {
        var result = RouteBuilder.GetServicePrefix(typeof(UserService));

        result.Should().Be("user-service");
    }

    [Fact]
    public void GetServicePrefix_WithServiceRouteAttribute_ReturnsAttributeValue()
    {
        var result = RouteBuilder.GetServicePrefix(typeof(ICustomRouteService));

        result.Should().Be("api/v2/custom");
    }

    [Fact]
    public void GetServicePrefix_ComplexName_ConvertsCorrectly()
    {
        var result = RouteBuilder.GetServicePrefix(typeof(IUserManagementService));

        result.Should().Be("user-management-service");
    }

    #endregion

    #region BuildRequest Tests

    [Fact]
    public void BuildRequest_SimpleGetWithId_BuildsQueryString()
    {
        var method = typeof(IUserService).GetMethod(nameof(IUserService.GetUserAsync))!;
        var args = new object?[] { 123 };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "user-service");

        url.Should().Be("/user-service/get-user?id=123");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_GetWithMultipleParams_BuildsQueryString()
    {
        var method = typeof(IUserService).GetMethod(nameof(IUserService.FindUsersAsync))!;
        var args = new object?[] { "john", 10 };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "user-service");

        url.Should().Be("/user-service/find-users?name=john&limit=10");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_GetWithNullParam_SkipsNullInQueryString()
    {
        var method = typeof(IUserService).GetMethod(nameof(IUserService.FindUsersAsync))!;
        var args = new object?[] { null, 10 };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "user-service");

        url.Should().Be("/user-service/find-users?limit=10");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_PostWithComplexType_SetsBody()
    {
        var method = typeof(IUserService).GetMethod(nameof(IUserService.CreateUserAsync))!;
        var request = new CreateUserRequest { Name = "John", Email = "john@example.com" };
        var args = new object?[] { request };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "user-service");

        url.Should().Be("/user-service/create-user");
        body.Should().BeSameAs(request);
    }

    [Fact]
    public void BuildRequest_WithCancellationToken_IgnoresToken()
    {
        var method = typeof(IUserService).GetMethod(nameof(IUserService.GetUserWithCancellationAsync))!;
        var args = new object?[] { 123, CancellationToken.None };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "user-service");

        url.Should().Be("/user-service/get-user-with-cancellation?id=123");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_WithRouteTemplate_SubstitutesPlaceholders()
    {
        var method = typeof(IServiceWithAttributes).GetMethod(nameof(IServiceWithAttributes.GetByIdAsync))!;
        var args = new object?[] { 456 };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "custom-service");

        url.Should().Be("/custom-service/items/456");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_AsyncMethodSuffix_RemovesAsyncFromPath()
    {
        var method = typeof(IUserService).GetMethod(nameof(IUserService.GetUserAsync))!;
        var args = new object?[] { 1 };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "user-service");

        url.Should().Contain("/get-user");
        url.Should().NotContain("async");
    }

    [Fact]
    public void BuildRequest_GetWithComplexType_ExtractsPropertiesToQueryString()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.GetUsersAsync))!;
        var query = new SearchQuery { Name = "john", Page = 1, PageSize = 10 };
        var args = new object?[] { query };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/get-users?Name=john&Page=1&PageSize=10");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_GetWithComplexType_SkipsNullProperties()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.GetUsersAsync))!;
        var query = new SearchQuery { Name = null, Page = 1, PageSize = 10 };
        var args = new object?[] { query };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/get-users?Page=1&PageSize=10");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_GetWithComplexType_NullObject_NoQueryString()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.GetUsersAsync))!;
        var args = new object?[] { null };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/get-users");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_DeleteWithComplexType_ExtractsPropertiesToQueryString()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.DeleteUsersAsync))!;
        var query = new DeleteQuery { Status = "inactive", OlderThanDays = 30 };
        var args = new object?[] { query };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/delete-users?Status=inactive&OlderThanDays=30");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_GetWithRouteTemplateAndComplexType_UsesPropertyInRoute()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.GetUserOrdersAsync))!;
        var query = new UserOrdersQuery { UserId = 123, Status = "pending", Page = 1 };
        var args = new object?[] { query };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/users/123/orders?Status=pending&Page=1");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_DeleteWithRouteTemplateAndComplexType_UsesPropertyInRoute()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.DeleteUserOrdersAsync))!;
        var query = new DeleteUserOrdersQuery { UserId = 456, Status = "cancelled" };
        var args = new object?[] { query };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/users/456/orders?Status=cancelled");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_GetWithComplexType_SkipsComplexNestedProperties()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.GetWithNestedAsync))!;
        var query = new QueryWithNestedType { Id = 1, Name = "test", Nested = new NestedType { Value = "ignored" } };
        var args = new object?[] { query };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/get-with-nested?Id=1&Name=test");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_GetWithComplexTypeAndSimpleParams_CombinesBoth()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.GetFilteredAsync))!;
        var query = new SearchQuery { Name = "john", Page = 1, PageSize = 10 };
        var args = new object?[] { query, "active" };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/get-filtered?Name=john&Page=1&PageSize=10&status=active");
        body.Should().BeNull();
    }

    [Fact]
    public void BuildRequest_PostWithComplexType_StillUsesBody()
    {
        var method = typeof(ISearchService).GetMethod(nameof(ISearchService.CreateSearchAsync))!;
        var request = new SearchQuery { Name = "john", Page = 1, PageSize = 10 };
        var args = new object?[] { request };

        var (url, body) = RouteBuilder.BuildRequest(method, args, "search-service");

        url.Should().Be("/search-service/create-search");
        body.Should().BeSameAs(request);
    }

    #endregion

    #region Helper Methods

    private static MethodInfo CreateMethod(string name)
    {
        // Create a dynamic method representation for testing conventions
        return typeof(IDynamicTestService).GetMethod(name)
            ?? throw new InvalidOperationException($"Method {name} not found in IDynamicTestService");
    }

    #endregion

    #region Test Interfaces

    public interface IDynamicTestService
    {
        Task<Result<object>> GetUser();
        Task<Result<object>> GetUserAsync();
        Task<Result<object>> GetById();
        Task<Result<object>> FindUser();
        Task<Result<object>> FindAllUsers();
        Task<Result<object>> ListUsers();
        Task<Result<object>> ListAll();
        Task<Result<object>> CreateUser();
        Task<Result<object>> CreateUserAsync();
        Task<Result<object>> AddUser();
        Task<Result<object>> AddItem();
        Task<Result<object>> UpdateUser();
        Task<Result<object>> UpdateUserAsync();
        Task<Result<object>> UpdateById();
        Task<Result<object>> DeleteUser();
        Task<Result<object>> DeleteUserAsync();
        Task<Result<object>> RemoveUser();
        Task<Result<object>> RemoveItem();
        Task<Result<object>> ProcessOrder();
        Task<Result<object>> ExecuteCommand();
        Task<Result<object>> DoSomething();
    }

    public interface IUserService
    {
        Task<Result<User>> GetUserAsync(int id);
        Task<Result<List<User>>> FindUsersAsync(string? name, int? limit);
        Task<Result<User>> CreateUserAsync(CreateUserRequest request);
        Task<Result<User>> GetUserWithCancellationAsync(int id, CancellationToken cancellationToken);
    }

    public interface IServiceWithAttributes
    {
        [HttpGet]
        Task<Result<object>> CustomMethod();

        [HttpPost]
        Task<Result<object>> PostMethod();

        [HttpGet("items/{id}")]
        Task<Result<object>> GetByIdAsync(int id);
    }

    [ServiceRoute("api/v2/custom")]
    public interface ICustomRouteService
    {
        Task<Result<object>> GetAsync();
    }

    public interface IUserManagementService
    {
        Task<Result<object>> GetAsync();
    }

    // Non-interface for testing
    public class UserService { }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class CreateUserRequest
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public interface ISearchService
    {
        Task<Result<List<User>>> GetUsersAsync(SearchQuery query);
        Task<Result> DeleteUsersAsync(DeleteQuery query);

        [HttpGet("users/{UserId}/orders")]
        Task<Result<List<object>>> GetUserOrdersAsync(UserOrdersQuery query);

        [HttpDelete("users/{UserId}/orders")]
        Task<Result> DeleteUserOrdersAsync(DeleteUserOrdersQuery query);

        Task<Result<object>> GetWithNestedAsync(QueryWithNestedType query);
        Task<Result<List<User>>> GetFilteredAsync(SearchQuery query, string status);
        Task<Result<object>> CreateSearchAsync(SearchQuery request);
    }

    public class SearchQuery
    {
        public string? Name { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class DeleteQuery
    {
        public string? Status { get; set; }
        public int OlderThanDays { get; set; }
    }

    public class UserOrdersQuery
    {
        public int UserId { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; }
    }

    public class DeleteUserOrdersQuery
    {
        public int UserId { get; set; }
        public string? Status { get; set; }
    }

    public class QueryWithNestedType
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public NestedType? Nested { get; set; }
    }

    public class NestedType
    {
        public string? Value { get; set; }
    }

    #endregion
}
