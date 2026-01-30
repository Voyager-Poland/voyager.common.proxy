namespace Voyager.Common.Proxy.Server.Abstractions;

using System.Threading.Tasks;

/// <summary>
/// Interface for checking permissions before service method invocation.
/// Implement this interface to provide custom permission checking logic for a service.
/// </summary>
/// <typeparam name="TService">The service interface type.</typeparam>
/// <example>
/// <code>
/// public class VIPServicePermissionChecker : IServicePermissionChecker&lt;IVIPService&gt;
/// {
///     private readonly ActionModule _actionModule;
///
///     public async Task&lt;PermissionResult&gt; CheckPermissionAsync(PermissionContext context)
///     {
///         var identity = PilotIdentity.FromPrincipal(context.User);
///         var action = _actionModule.GetActionForMethod(context.Method.Name);
///         var result = await action.CheckPermissionsOnlyAsync(...);
///         return result.IsSuccess ? PermissionResult.Granted() : PermissionResult.Denied(result.Error.Message);
///     }
/// }
/// </code>
/// </example>
public interface IServicePermissionChecker<TService>
    where TService : class
{
    /// <summary>
    /// Checks if the user has permission to invoke the specified method.
    /// Called by middleware before dispatching to the service method.
    /// </summary>
    /// <param name="context">
    /// The permission context containing user, method, and parameter information.
    /// </param>
    /// <returns>
    /// A task that resolves to the permission result.
    /// Return <see cref="PermissionResult.Granted()"/> to allow the method call.
    /// Return <see cref="PermissionResult.Denied(string)"/> for 403 Forbidden.
    /// Return <see cref="PermissionResult.Unauthenticated(string)"/> for 401 Unauthorized.
    /// </returns>
    Task<PermissionResult> CheckPermissionAsync(PermissionContext context);
}
