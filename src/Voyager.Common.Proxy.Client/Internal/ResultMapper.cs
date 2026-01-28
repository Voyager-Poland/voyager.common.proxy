namespace Voyager.Common.Proxy.Client.Internal
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Voyager.Common.Results;

    /// <summary>
    /// Maps HTTP responses to Result types.
    /// </summary>
    internal static class ResultMapper
    {
        /// <summary>
        /// Maps an HTTP response to a Result type.
        /// </summary>
        public static async Task<object> MapResponseAsync(
            HttpResponseMessage response,
            Type resultType,
            JsonSerializerOptions jsonOptions)
        {
            // Determine if it's Result or Result<T>
            var isGeneric = resultType.IsGenericType &&
                           resultType.GetGenericTypeDefinition() == typeof(Result<>);

            if (response.IsSuccessStatusCode)
            {
                return await MapSuccessAsync(response, resultType, isGeneric, jsonOptions).ConfigureAwait(false);
            }
            else
            {
                return await MapErrorAsync(response, resultType, isGeneric).ConfigureAwait(false);
            }
        }

        private static async Task<object> MapSuccessAsync(
            HttpResponseMessage response,
            Type resultType,
            bool isGeneric,
            JsonSerializerOptions jsonOptions)
        {
            if (!isGeneric)
            {
                // Result (non-generic) - just return success
                return Result.Success();
            }

            // Result<T> - deserialize the value
            var valueType = resultType.GetGenericArguments()[0];

            // Handle 204 No Content
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return CreateSuccessResult(resultType, valueType, GetDefaultValue(valueType));
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(content))
            {
                return CreateSuccessResult(resultType, valueType, GetDefaultValue(valueType));
            }

            var value = JsonSerializer.Deserialize(content, valueType, jsonOptions);
            return CreateSuccessResult(resultType, valueType, value);
        }

        private static async Task<object> MapErrorAsync(
            HttpResponseMessage response,
            Type resultType,
            bool isGeneric)
        {
            var errorMessage = await TryReadErrorMessageAsync(response).ConfigureAwait(false);
            var error = MapStatusCodeToError(response.StatusCode, errorMessage);

            if (!isGeneric)
            {
                return Result.Failure(error);
            }

            // Result<T>.Failure(error)
            var valueType = resultType.GetGenericArguments()[0];
            return CreateFailureResult(resultType, valueType, error);
        }

        private static async Task<string> TryReadErrorMessageAsync(HttpResponseMessage response)
        {
            try
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(content))
                {
                    return response.ReasonPhrase ?? response.StatusCode.ToString();
                }

                // Try to parse as JSON with error/message field
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("error", out var errorProp))
                    {
                        return errorProp.GetString() ?? content;
                    }

                    if (root.TryGetProperty("message", out var messageProp))
                    {
                        return messageProp.GetString() ?? content;
                    }

                    if (root.TryGetProperty("title", out var titleProp))
                    {
                        return titleProp.GetString() ?? content;
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, use raw content
                }

                return content;
            }
            catch
            {
                return response.ReasonPhrase ?? response.StatusCode.ToString();
            }
        }

        private static Error MapStatusCodeToError(HttpStatusCode statusCode, string message)
        {
            return statusCode switch
            {
                HttpStatusCode.BadRequest => Error.ValidationError(message),
                HttpStatusCode.Unauthorized => Error.UnauthorizedError(message),
                HttpStatusCode.Forbidden => Error.PermissionError(message),
                HttpStatusCode.NotFound => Error.NotFoundError(message),
                HttpStatusCode.Conflict => Error.ConflictError(message),
                HttpStatusCode.RequestTimeout => Error.TimeoutError(message),
                HttpStatusCode.TooManyRequests => Error.UnavailableError(message),
                HttpStatusCode.ServiceUnavailable => Error.UnavailableError(message),
                HttpStatusCode.GatewayTimeout => Error.TimeoutError(message),
                _ when (int)statusCode >= 500 => Error.UnexpectedError(message),
                _ => Error.UnexpectedError($"HTTP {(int)statusCode}: {message}")
            };
        }

        private static object CreateSuccessResult(Type resultType, Type valueType, object? value)
        {
            // Result<T>.Success(value) via implicit conversion
            // We need to call the static Success method or use implicit conversion

            // Find implicit operator or Success method
            var successMethod = resultType.GetMethod(
                "Success",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { valueType },
                null);

            if (successMethod != null)
            {
                return successMethod.Invoke(null, new[] { value })!;
            }

            // Try implicit conversion from T to Result<T>
            var implicitOp = resultType.GetMethod(
                "op_Implicit",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { valueType },
                null);

            if (implicitOp != null)
            {
                return implicitOp.Invoke(null, new[] { value })!;
            }

            throw new InvalidOperationException(
                $"Cannot create success Result<{valueType.Name}>. " +
                $"No Success method or implicit conversion found.");
        }

        private static object CreateFailureResult(Type resultType, Type valueType, Error error)
        {
            // Result<T>.Failure(error)
            var failureMethod = resultType.GetMethod(
                "Failure",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Error) },
                null);

            if (failureMethod != null)
            {
                return failureMethod.Invoke(null, new object[] { error })!;
            }

            // Try implicit conversion from Error to Result<T>
            var implicitOp = resultType.GetMethod(
                "op_Implicit",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Error) },
                null);

            if (implicitOp != null)
            {
                return implicitOp.Invoke(null, new object[] { error })!;
            }

            throw new InvalidOperationException(
                $"Cannot create failure Result<{valueType.Name}>. " +
                $"No Failure method or implicit conversion found.");
        }

        private static object? GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
