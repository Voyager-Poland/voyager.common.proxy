namespace Voyager.Common.Proxy.Abstractions
{
    /// <summary>
    /// HTTP methods supported by the proxy.
    /// </summary>
    public enum HttpMethod
    {
        /// <summary>
        /// HTTP GET method - used for retrieving resources.
        /// </summary>
        Get,

        /// <summary>
        /// HTTP POST method - used for creating resources or executing actions.
        /// </summary>
        Post,

        /// <summary>
        /// HTTP PUT method - used for replacing resources.
        /// </summary>
        Put,

        /// <summary>
        /// HTTP DELETE method - used for removing resources.
        /// </summary>
        Delete,

        /// <summary>
        /// HTTP PATCH method - used for partial updates.
        /// </summary>
        Patch
    }
}
