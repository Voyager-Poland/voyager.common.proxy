namespace Voyager.Common.Proxy.Server.Swagger.Core.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Platform-agnostic representation of a JSON Schema.
/// </summary>
public sealed class SchemaDefinition
{
    /// <summary>
    /// Gets the JSON Schema type (string, integer, number, boolean, array, object).
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Gets the format (e.g., int32, int64, float, double, date-time, uuid).
    /// </summary>
    public string? Format { get; }

    /// <summary>
    /// Gets a value indicating whether the value can be null.
    /// </summary>
    public bool Nullable { get; }

    /// <summary>
    /// Gets the reference to another schema (e.g., "#/components/schemas/User").
    /// </summary>
    public string? Reference { get; }

    /// <summary>
    /// Gets the schema for array items.
    /// </summary>
    public SchemaDefinition? Items { get; }

    /// <summary>
    /// Gets the properties for object types.
    /// </summary>
    public IReadOnlyDictionary<string, SchemaDefinition>? Properties { get; }

    /// <summary>
    /// Gets the required property names for object types.
    /// </summary>
    public IReadOnlyList<string>? Required { get; }

    /// <summary>
    /// Gets the enum values.
    /// </summary>
    public IReadOnlyList<string>? EnumValues { get; }

    /// <summary>
    /// Gets the .NET type this schema was generated from.
    /// </summary>
    public Type? ClrType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaDefinition"/> class for primitive types.
    /// </summary>
    public SchemaDefinition(string type, string? format = null, bool nullable = false, Type? clrType = null)
    {
        Type = type;
        Format = format;
        Nullable = nullable;
        ClrType = clrType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaDefinition"/> class for reference types.
    /// </summary>
    public SchemaDefinition(string reference, Type? clrType = null)
    {
        Reference = reference;
        ClrType = clrType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaDefinition"/> class for array types.
    /// </summary>
    public SchemaDefinition(SchemaDefinition items, bool nullable = false, Type? clrType = null)
    {
        Type = "array";
        Items = items;
        Nullable = nullable;
        ClrType = clrType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaDefinition"/> class for object types.
    /// </summary>
    public SchemaDefinition(
        IReadOnlyDictionary<string, SchemaDefinition> properties,
        IReadOnlyList<string>? required = null,
        bool nullable = false,
        Type? clrType = null)
    {
        Type = "object";
        Properties = properties;
        Required = required;
        Nullable = nullable;
        ClrType = clrType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaDefinition"/> class for enum types.
    /// </summary>
    public SchemaDefinition(IReadOnlyList<string> enumValues, bool nullable = false, Type? clrType = null)
    {
        Type = "string";
        EnumValues = enumValues;
        Nullable = nullable;
        ClrType = clrType;
    }

    /// <summary>
    /// Gets a value indicating whether this is a reference to another schema.
    /// </summary>
    public bool IsReference => Reference != null;

    /// <summary>
    /// Gets the schema name for reference (e.g., "User" from "#/components/schemas/User").
    /// </summary>
    public string? GetReferenceName()
    {
        if (Reference == null) return null;
        var lastSlash = Reference.LastIndexOf('/');
        return lastSlash >= 0 ? Reference.Substring(lastSlash + 1) : Reference;
    }
}
