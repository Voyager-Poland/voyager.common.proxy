namespace Voyager.Common.Proxy.Server.Abstractions;

using System;

/// <summary>
/// Describes a parameter of a service method endpoint.
/// </summary>
public sealed class ParameterDescriptor
{
    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the parameter.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the source of the parameter value.
    /// </summary>
    public ParameterSource Source { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter is optional.
    /// </summary>
    public bool IsOptional { get; }

    /// <summary>
    /// Gets the default value for optional parameters.
    /// </summary>
    public object? DefaultValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterDescriptor"/> class.
    /// </summary>
    public ParameterDescriptor(
        string name,
        Type type,
        ParameterSource source,
        bool isOptional = false,
        object? defaultValue = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Source = source;
        IsOptional = isOptional;
        DefaultValue = defaultValue;
    }
}
