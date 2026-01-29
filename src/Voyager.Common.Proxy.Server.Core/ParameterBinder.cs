namespace Voyager.Common.Proxy.Server.Core;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Binds parameter values from the request context.
/// </summary>
public class ParameterBinder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Binds parameter values from the request context based on endpoint descriptor.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <param name="endpoint">The endpoint descriptor.</param>
    /// <returns>An array of parameter values in order.</returns>
    public async Task<object?[]> BindParametersAsync(IRequestContext context, EndpointDescriptor endpoint)
    {
        var parameters = endpoint.Parameters;
        var values = new object?[parameters.Count];

        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            values[i] = await BindParameterAsync(context, param);
        }

        return values;
    }

    private async Task<object?> BindParameterAsync(IRequestContext context, ParameterDescriptor param)
    {
        switch (param.Source)
        {
            case ParameterSource.Route:
                return BindFromRoute(context, param);

            case ParameterSource.Query:
                return BindFromQuery(context, param);

            case ParameterSource.Body:
                return await BindFromBodyAsync(context, param);

            case ParameterSource.CancellationToken:
                return context.CancellationToken;

            case ParameterSource.RouteAndQuery:
                return BindFromRouteAndQuery(context, param);

            default:
                throw new ArgumentOutOfRangeException(nameof(param.Source));
        }
    }

    private static object? BindFromRoute(IRequestContext context, ParameterDescriptor param)
    {
        if (context.RouteValues.TryGetValue(param.Name, out var value))
        {
            return ConvertValue(value, param.Type);
        }

        if (param.IsOptional)
        {
            return param.DefaultValue;
        }

        throw new ArgumentException($"Required route parameter '{param.Name}' not found.");
    }

    private static object? BindFromQuery(IRequestContext context, ParameterDescriptor param)
    {
        if (context.QueryParameters.TryGetValue(param.Name, out var value))
        {
            return ConvertValue(value, param.Type);
        }

        if (param.IsOptional)
        {
            return param.DefaultValue;
        }

        // Query parameters are optional by default for nullable types
        if (IsNullableType(param.Type))
        {
            return null;
        }

        return param.DefaultValue;
    }

    private static object? BindFromRouteAndQuery(IRequestContext context, ParameterDescriptor param)
    {
        // Create instance of the complex type
        var instance = Activator.CreateInstance(param.Type);
        if (instance == null)
        {
            return param.IsOptional ? param.DefaultValue : null;
        }

        // Get all settable properties
        var properties = param.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite);

        foreach (var property in properties)
        {
            string? stringValue = null;

            // Route values take precedence over query parameters
            if (context.RouteValues.TryGetValue(property.Name, out var routeValue))
            {
                stringValue = routeValue;
            }
            else if (context.QueryParameters.TryGetValue(property.Name, out var queryValue))
            {
                stringValue = queryValue;
            }

            if (stringValue != null)
            {
                try
                {
                    var convertedValue = ConvertValue(stringValue, property.PropertyType);
                    property.SetValue(instance, convertedValue);
                }
                catch (ArgumentException)
                {
                    // Skip properties that can't be converted
                }
            }
        }

        return instance;
    }

    private static async Task<object?> BindFromBodyAsync(IRequestContext context, ParameterDescriptor param)
    {
        if (context.Body == null)
        {
            if (param.IsOptional)
            {
                return param.DefaultValue;
            }

            return null;
        }

        try
        {
            // Reset stream position if possible
            if (context.Body.CanSeek)
            {
                context.Body.Position = 0;

                // Check if stream is empty
                if (context.Body.Length == 0)
                {
                    return param.IsOptional ? param.DefaultValue : null;
                }
            }

            var result = await JsonSerializer.DeserializeAsync(context.Body, param.Type, JsonOptions, context.CancellationToken);

            if (result == null && param.IsOptional)
            {
                return param.DefaultValue;
            }

            return result;
        }
        catch (JsonException ex) when (IsEmptyStreamException(ex))
        {
            // Empty stream - return null or default
            return param.IsOptional ? param.DefaultValue : null;
        }
        catch (JsonException)
        {
            throw new ArgumentException($"Failed to deserialize request body to type '{param.Type.Name}'.");
        }
    }

    private static bool IsEmptyStreamException(JsonException ex)
    {
        // JsonSerializer throws specific messages for empty input
        return ex.Message.Contains("input does not contain any JSON tokens") ||
               ex.Message.Contains("The input does not contain any JSON tokens") ||
               ex.BytePositionInLine == 0 && ex.LineNumber == 0;
    }

    private static object? ConvertValue(string value, Type targetType)
    {
        if (string.IsNullOrEmpty(value))
        {
            return GetDefaultValue(targetType);
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            targetType = underlyingType;
        }

        // Direct string assignment
        if (targetType == typeof(string))
        {
            return value;
        }

        // Use TypeConverter for conversion
        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(value);
        }

        // Fallback: try parsing common types
        if (targetType == typeof(int) && int.TryParse(value, out var intVal))
        {
            return intVal;
        }

        if (targetType == typeof(long) && long.TryParse(value, out var longVal))
        {
            return longVal;
        }

        if (targetType == typeof(decimal) && decimal.TryParse(value, out var decVal))
        {
            return decVal;
        }

        if (targetType == typeof(double) && double.TryParse(value, out var dblVal))
        {
            return dblVal;
        }

        if (targetType == typeof(bool) && bool.TryParse(value, out var boolVal))
        {
            return boolVal;
        }

        if (targetType == typeof(Guid) && Guid.TryParse(value, out var guidVal))
        {
            return guidVal;
        }

        if (targetType.IsEnum)
        {
            try
            {
                return Enum.Parse(targetType, value, ignoreCase: true);
            }
            catch (ArgumentException)
            {
                // Fall through to throw below
            }
        }

        throw new ArgumentException($"Cannot convert value '{value}' to type '{targetType.Name}'.");
    }

    private static bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        return null;
    }
}
