namespace Voyager.Common.Proxy.Server.Abstractions;

using System;

/// <summary>
/// Result of a permission check.
/// </summary>
public sealed class PermissionResult
{
    /// <summary>
    /// Gets a value indicating whether permission was granted.
    /// </summary>
    public bool IsGranted { get; }

    /// <summary>
    /// Gets the denial reason (if permission was denied).
    /// </summary>
    public string? DenialReason { get; }

    /// <summary>
    /// Gets a value indicating whether this is an authentication failure (401)
    /// rather than an authorization failure (403).
    /// </summary>
    public bool IsAuthenticationFailure { get; }

    private PermissionResult(bool isGranted, string? denialReason, bool isAuthenticationFailure)
    {
        IsGranted = isGranted;
        DenialReason = denialReason;
        IsAuthenticationFailure = isAuthenticationFailure;
    }

    /// <summary>
    /// Creates a successful permission result (permission granted).
    /// </summary>
    public static PermissionResult Granted() => new PermissionResult(true, null, false);

    /// <summary>
    /// Creates a denied permission result with 403 Forbidden status.
    /// Use this when the user is authenticated but not authorized.
    /// </summary>
    /// <param name="reason">The reason for denial.</param>
    public static PermissionResult Denied(string reason) =>
        new PermissionResult(false, reason ?? "Permission denied", false);

    /// <summary>
    /// Creates a denied permission result with 401 Unauthorized status.
    /// Use this when the user is not authenticated.
    /// </summary>
    /// <param name="reason">The reason for denial.</param>
    public static PermissionResult Unauthenticated(string? reason = null) =>
        new PermissionResult(false, reason ?? "Authentication required", true);

    /// <summary>
    /// Implicitly converts a boolean to a PermissionResult.
    /// true = Granted, false = Denied with default message.
    /// </summary>
    public static implicit operator PermissionResult(bool granted) =>
        granted ? Granted() : Denied("Permission denied");
}
