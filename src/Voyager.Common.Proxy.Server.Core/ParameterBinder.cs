namespace Voyager.Common.Proxy.Server.Core;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
            }

            var result = await JsonSerializer.DeserializeAsync(context.Body, param.Type, JsonOptions, context.CancellationToken);

            if (result == null && param.IsOptional)
            {
                return param.DefaultValue;
            }

            return result;
        }
        catch (JsonException)
        {
            throw new ArgumentException($"Failed to deserialize request body to type '{param.Type.Name}'.");
        }
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
