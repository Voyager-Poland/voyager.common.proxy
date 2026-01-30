namespace Voyager.Common.Proxy.Server.Abstractions;

using System;
using System.Threading.Tasks;

/// <summary>
/// Base options for configuring service proxy middleware.
/// Platform-specific implementations add context-aware factory support.
/// </summary>
/// <typeparam name="TService">The service interface type.</typeparam>
public abstract class ServiceProxyOptionsBase<TService>
    where TService : class
{
    /// <summary>
    /// Gets or sets the factory function to create service instances.
    /// This is the simplest option - use when the service doesn't need request context.
    /// </summary>
    public Func<TService>? ServiceFactory { get; set; }

    /// <summary>
    /// Gets or sets the permission checker callback.
    /// Called before each method invocation to check if the user has permission.
    /// Use this for simple inline permission logic.
    /// </summary>
    /// <example>
    /// <code>
    /// options.PermissionChecker = async ctx =>
    /// {
    ///     if (ctx.User?.Identity?.IsAuthenticated != true)
    ///         return PermissionResult.Unauthenticated();
    ///
    ///     if (ctx.Method.Name == "DeleteAsync" &amp;&amp; !ctx.User.IsInRole("Admin"))
    ///         return PermissionResult.Denied("Admin role required");
    ///
    ///     return PermissionResult.Granted();
    /// };
    /// </code>
    /// </example>
    public Func<PermissionContext, Task<PermissionResult>>? PermissionChecker { get; set; }

    /// <summary>
    /// Gets or sets a typed permission checker instance.
    /// Use this for complex permission logic that benefits from a dedicated class.
    /// Takes precedence over <see cref="PermissionChecker"/> if both are set.
    /// </summary>
    public IServicePermissionChecker<TService>? PermissionCheckerInstance { get; set; }

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    public virtual void Validate()
    {
        // ServiceFactory validation is done in platform-specific classes
        // since they may have ContextAwareFactory as alternative
    }

    /// <summary>
    /// Gets the effective permission checker function.
    /// Returns null if no permission checking is configured.
    /// </summary>
    public Func<PermissionContext, Task<PermissionResult>>? GetEffectivePermissionChecker()
    {
        // Instance takes precedence
        if (PermissionCheckerInstance != null)
        {
            return PermissionCheckerInstance.CheckPermissionAsync;
        }

        return PermissionChecker;
    }
}
