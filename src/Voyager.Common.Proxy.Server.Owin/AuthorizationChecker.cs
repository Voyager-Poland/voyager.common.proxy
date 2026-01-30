namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Checks authorization for service proxy endpoints in OWIN.
/// </summary>
internal static class AuthorizationChecker
{
    /// <summary>
    /// Determines the authorization requirements for an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint descriptor.</param>
    /// <returns>The authorization info for the endpoint.</returns>
    public static AuthorizationInfo GetAuthorizationInfo(EndpointDescriptor endpoint)
    {
        // Check for AllowAnonymous on method first
        if (endpoint.Method.GetCustomAttribute<AllowAnonymousAttribute>() != null)
        {
            return AuthorizationInfo.AllowAnonymous();
        }

        // Get method-level authorization attributes
        var methodAttributes = endpoint.Method.GetCustomAttributes<RequireAuthorizationAttribute>().ToList();
        if (methodAttributes.Count > 0)
        {
            return AuthorizationInfo.FromAttributes(methodAttributes);
        }

        // Get interface-level authorization attributes
        var interfaceAttributes = endpoint.ServiceType.GetCustomAttributes<RequireAuthorizationAttribute>().ToList();
        if (interfaceAttributes.Count > 0)
        {
            return AuthorizationInfo.FromAttributes(interfaceAttributes);
        }

        // No authorization required
        return AuthorizationInfo.AllowAnonymous();
    }

    /// <summary>
    /// Checks if the user is authorized to access the endpoint.
    /// </summary>
    /// <param name="environment">The OWIN environment dictionary.</param>
    /// <param name="authInfo">The authorization info for the endpoint.</param>
    /// <returns>The authorization result.</returns>
    public static AuthorizationResult CheckAuthorization(
        IDictionary<string, object> environment,
        AuthorizationInfo authInfo)
    {
        if (!authInfo.RequiresAuthorization)
        {
            return AuthorizationResult.Success();
        }

        // Get user from OWIN environment
        var user = GetUser(environment);

        if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
        {
            return AuthorizationResult.Unauthorized("User is not authenticated.");
        }

        // Check roles if specified
        if (authInfo.Roles != null && authInfo.Roles.Length > 0)
        {
            var hasRole = authInfo.Roles.Any(role => user.IsInRole(role));
            if (!hasRole)
            {
                return AuthorizationResult.Forbidden($"User does not have required role(s): {string.Join(", ", authInfo.Roles)}");
            }
        }

        // Note: Policy-based authorization is not directly supported in OWIN
        // as it's an ASP.NET Core concept. If policies are specified,
        // we'll require authentication only.
        // Custom policy handlers would need to be implemented separately.

        return AuthorizationResult.Success();
    }

    private static IPrincipal? GetUser(IDictionary<string, object> environment)
    {
        // Try different keys where the user might be stored
        if (environment.TryGetValue("server.User", out var serverUser) && serverUser is IPrincipal principal1)
        {
            return principal1;
        }

        if (environment.TryGetValue("owin.User", out var owinUser) && owinUser is IPrincipal principal2)
        {
            return principal2;
        }

        // For Microsoft.Owin (Katana)
        if (environment.TryGetValue("Microsoft.Owin.Security.User", out var katanaUser) && katanaUser is IPrincipal principal3)
        {
            return principal3;
        }

        return null;
    }
}

/// <summary>
/// Contains authorization requirements for an endpoint.
/// </summary>
internal sealed class AuthorizationInfo
{
    /// <summary>
    /// Gets a value indicating whether authorization is required.
    /// </summary>
    public bool RequiresAuthorization { get; }

    /// <summary>
    /// Gets the required roles (if any). User must have at least one of these roles.
    /// </summary>
    public string[]? Roles { get; }

    /// <summary>
    /// Gets the required authentication schemes (if any).
    /// </summary>
    public string[]? AuthenticationSchemes { get; }

    /// <summary>
    /// Gets the required policies (if any).
    /// Note: Policy-based authorization is limited in OWIN.
    /// </summary>
    public string[]? Policies { get; }

    private AuthorizationInfo(
        bool requiresAuthorization,
        string[]? roles = null,
        string[]? authenticationSchemes = null,
        string[]? policies = null)
    {
        RequiresAuthorization = requiresAuthorization;
        Roles = roles;
        AuthenticationSchemes = authenticationSchemes;
        Policies = policies;
    }

    public static AuthorizationInfo AllowAnonymous() => new AuthorizationInfo(false);

    public static AuthorizationInfo FromAttributes(IList<RequireAuthorizationAttribute> attributes)
    {
        if (attributes.Count == 0)
        {
            return AllowAnonymous();
        }

        var roles = attributes
            .Where(a => !string.IsNullOrEmpty(a.Roles))
            .SelectMany(a => a.Roles!.Split(',').Select(r => r.Trim()))
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct()
            .ToArray();

        var schemes = attributes
            .Where(a => !string.IsNullOrEmpty(a.AuthenticationSchemes))
            .SelectMany(a => a.AuthenticationSchemes!.Split(',').Select(s => s.Trim()))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToArray();

        var policies = attributes
            .Where(a => !string.IsNullOrEmpty(a.Policy))
            .Select(a => a.Policy!)
            .Distinct()
            .ToArray();

        return new AuthorizationInfo(
            true,
            roles.Length > 0 ? roles : null,
            schemes.Length > 0 ? schemes : null,
            policies.Length > 0 ? policies : null);
    }
}

/// <summary>
/// Result of an authorization check.
/// </summary>
internal sealed class AuthorizationResult
{
    /// <summary>
    /// Gets a value indicating whether authorization succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets a value indicating whether the user is not authenticated (401).
    /// </summary>
    public bool IsUnauthorized { get; }

    /// <summary>
    /// Gets a value indicating whether the user is authenticated but not allowed (403).
    /// </summary>
    public bool IsForbidden { get; }

    /// <summary>
    /// Gets the failure reason (if authorization failed).
    /// </summary>
    public string? FailureReason { get; }

    private AuthorizationResult(bool succeeded, bool isUnauthorized, bool isForbidden, string? failureReason)
    {
        Succeeded = succeeded;
        IsUnauthorized = isUnauthorized;
        IsForbidden = isForbidden;
        FailureReason = failureReason;
    }

    public static AuthorizationResult Success() =>
        new AuthorizationResult(true, false, false, null);

    public static AuthorizationResult Unauthorized(string reason) =>
        new AuthorizationResult(false, true, false, reason);

    public static AuthorizationResult Forbidden(string reason) =>
        new AuthorizationResult(false, false, true, reason);
}
