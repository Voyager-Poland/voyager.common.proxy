namespace Voyager.Common.Proxy.Server.Core;

using System;
using System.Linq;
using System.Reflection;
using Voyager.Common.Proxy.Abstractions.Validation;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Results;

/// <summary>
/// Validates request parameters that implement validation interfaces or have validation methods.
/// </summary>
internal static class RequestValidator
{
    /// <summary>
    /// Checks if validation should be performed for the given endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint descriptor.</param>
    /// <returns>True if validation should be performed.</returns>
    public static bool ShouldValidate(EndpointDescriptor endpoint)
    {
        return endpoint.Method.GetCustomAttribute<ValidateRequestAttribute>() != null ||
               endpoint.ServiceType.GetCustomAttribute<ValidateRequestAttribute>() != null;
    }

    /// <summary>
    /// Validates all parameters that implement validation interfaces or have validation methods.
    /// </summary>
    /// <param name="parameters">The parameter values to validate.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static void ValidateParameters(object?[] parameters)
    {
        foreach (var param in parameters)
        {
            if (param == null) continue;

            // Approach 1: Interface (fast - no reflection)
            if (param is IValidatableRequest validatable)
            {
                var result = validatable.IsValid();
                if (!result.IsSuccess)
                {
                    throw new ArgumentException(result.Error.Message);
                }
                continue;
            }

            if (param is IValidatableRequestBool validatableBool)
            {
                if (!validatableBool.IsValid())
                {
                    throw new ArgumentException(
                        validatableBool.ValidationErrorMessage ?? "Request validation failed");
                }
                continue;
            }

            // Approach 2: Attribute [ValidationMethod] (slower - reflection)
            var validationMethod = param.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.GetCustomAttribute<ValidationMethodAttribute>() != null);

            if (validationMethod != null)
            {
                ValidateWithAttributeMethod(param, validationMethod);
            }
        }
    }

    private static void ValidateWithAttributeMethod(object param, MethodInfo method)
    {
        var attr = method.GetCustomAttribute<ValidationMethodAttribute>()!;
        var returnType = method.ReturnType;

        // Validate method signature
        if (method.GetParameters().Length > 0)
        {
            throw new InvalidOperationException(
                $"[ValidationMethod] must have no parameters, but {method.DeclaringType?.Name}.{method.Name} has {method.GetParameters().Length} parameters");
        }

        object? result;
        try
        {
            result = method.Invoke(param, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new ArgumentException($"Validation method threw an exception: {ex.InnerException.Message}", ex.InnerException);
        }

        // Handle Result return type
        if (typeof(Result).IsAssignableFrom(returnType))
        {
            if (result is Result resultValue && !resultValue.IsSuccess)
            {
                throw new ArgumentException(resultValue.Error.Message);
            }
        }
        // Handle bool return type
        else if (returnType == typeof(bool))
        {
            if (result is bool boolValue && !boolValue)
            {
                throw new ArgumentException(attr.ErrorMessage ?? "Request validation failed");
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"[ValidationMethod] must return Result or bool, but {method.DeclaringType?.Name}.{method.Name} returns {returnType.Name}");
        }
    }
}
