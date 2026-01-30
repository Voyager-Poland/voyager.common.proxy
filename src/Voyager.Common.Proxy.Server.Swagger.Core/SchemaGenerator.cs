namespace Voyager.Common.Proxy.Server.Swagger.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Voyager.Common.Proxy.Server.Swagger.Core.Models;

/// <summary>
/// Generates JSON Schema definitions from .NET types.
/// </summary>
public class SchemaGenerator
{
    private readonly Dictionary<Type, SchemaDefinition> _schemaCache = new Dictionary<Type, SchemaDefinition>();
    private readonly Dictionary<string, SchemaDefinition> _componentSchemas = new Dictionary<string, SchemaDefinition>();

    /// <summary>
    /// Gets the component schemas generated during schema generation.
    /// These should be added to the OpenAPI document's components/schemas section.
    /// </summary>
    public IReadOnlyDictionary<string, SchemaDefinition> ComponentSchemas => _componentSchemas;

    /// <summary>
    /// Generates a schema definition for the specified type.
    /// </summary>
    /// <param name="type">The type to generate schema for.</param>
    /// <returns>The schema definition.</returns>
    public SchemaDefinition GenerateSchema(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return GenerateSchemaInternal(type, new HashSet<Type>());
    }

    private SchemaDefinition GenerateSchemaInternal(Type type, HashSet<Type> visitedTypes)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        var isNullable = underlyingType != null;
        if (underlyingType != null)
        {
            type = underlyingType;
        }

        // Check cache first
        if (_schemaCache.TryGetValue(type, out var cached))
        {
            return isNullable ? WithNullable(cached) : cached;
        }

        // Handle primitive types
        var primitiveSchema = TryGetPrimitiveSchema(type, isNullable);
        if (primitiveSchema != null)
        {
            return primitiveSchema;
        }

        // Handle enums
        if (type.IsEnum)
        {
            var enumSchema = new SchemaDefinition(
                Enum.GetNames(type).ToList(),
                isNullable,
                type);
            _schemaCache[type] = enumSchema;
            return enumSchema;
        }

        // Handle arrays and collections
        var elementType = GetEnumerableElementType(type);
        if (elementType != null)
        {
            var itemSchema = GenerateSchemaInternal(elementType, visitedTypes);
            var arraySchema = new SchemaDefinition(itemSchema, isNullable, type);
            return arraySchema;
        }

        // Handle complex types - generate reference
        return GenerateComplexTypeSchema(type, isNullable, visitedTypes);
    }

    private SchemaDefinition? TryGetPrimitiveSchema(Type type, bool nullable)
    {
        if (type == typeof(string))
            return new SchemaDefinition("string", null, nullable, type);

        if (type == typeof(bool))
            return new SchemaDefinition("boolean", null, nullable, type);

        if (type == typeof(byte))
            return new SchemaDefinition("integer", "int32", nullable, type);

        if (type == typeof(sbyte))
            return new SchemaDefinition("integer", "int32", nullable, type);

        if (type == typeof(short))
            return new SchemaDefinition("integer", "int32", nullable, type);

        if (type == typeof(ushort))
            return new SchemaDefinition("integer", "int32", nullable, type);

        if (type == typeof(int))
            return new SchemaDefinition("integer", "int32", nullable, type);

        if (type == typeof(uint))
            return new SchemaDefinition("integer", "int32", nullable, type);

        if (type == typeof(long))
            return new SchemaDefinition("integer", "int64", nullable, type);

        if (type == typeof(ulong))
            return new SchemaDefinition("integer", "int64", nullable, type);

        if (type == typeof(float))
            return new SchemaDefinition("number", "float", nullable, type);

        if (type == typeof(double))
            return new SchemaDefinition("number", "double", nullable, type);

        if (type == typeof(decimal))
            return new SchemaDefinition("number", "double", nullable, type);

        if (type == typeof(DateTime))
            return new SchemaDefinition("string", "date-time", nullable, type);

        if (type == typeof(DateTimeOffset))
            return new SchemaDefinition("string", "date-time", nullable, type);

        if (type == typeof(TimeSpan))
            return new SchemaDefinition("string", "duration", nullable, type);

        if (type == typeof(Guid))
            return new SchemaDefinition("string", "uuid", nullable, type);

        if (type == typeof(Uri))
            return new SchemaDefinition("string", "uri", nullable, type);

        if (type == typeof(byte[]))
            return new SchemaDefinition("string", "byte", nullable, type);

        if (type == typeof(object))
            return new SchemaDefinition("object", null, nullable, type);

        return null;
    }

    private Type? GetEnumerableElementType(Type type)
    {
        // Handle arrays
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        // Handle generic collections
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();

            if (genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(List<>) ||
                genericDef == typeof(IReadOnlyList<>) ||
                genericDef == typeof(IReadOnlyCollection<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        // Check if implements IEnumerable<T>
        var enumerableInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface != null && type != typeof(string))
        {
            return enumerableInterface.GetGenericArguments()[0];
        }

        return null;
    }

    private SchemaDefinition GenerateComplexTypeSchema(Type type, bool nullable, HashSet<Type> visitedTypes)
    {
        var schemaName = GetSchemaName(type);

        // Check if already in components
        if (_componentSchemas.ContainsKey(schemaName))
        {
            return new SchemaDefinition($"#/components/schemas/{schemaName}", type);
        }

        // Check for circular reference
        if (visitedTypes.Contains(type))
        {
            return new SchemaDefinition($"#/components/schemas/{schemaName}", type);
        }

        visitedTypes.Add(type);

        // Generate object schema
        var properties = new Dictionary<string, SchemaDefinition>();
        var requiredProps = new List<string>();

        var allProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        foreach (var prop in allProperties)
        {
            var propName = ToCamelCase(prop.Name);
            var propSchema = GenerateSchemaInternal(prop.PropertyType, visitedTypes);
            properties[propName] = propSchema;

            // Check if required (non-nullable value type or has Required attribute)
            if (IsPropertyRequired(prop))
            {
                requiredProps.Add(propName);
            }
        }

        var objectSchema = new SchemaDefinition(
            properties,
            requiredProps.Count > 0 ? requiredProps : null,
            false,
            type);

        // Add to components
        _componentSchemas[schemaName] = objectSchema;

        // Return reference
        var refSchema = new SchemaDefinition($"#/components/schemas/{schemaName}", type);
        _schemaCache[type] = refSchema;

        visitedTypes.Remove(type);

        return refSchema;
    }

    private static bool IsPropertyRequired(PropertyInfo property)
    {
        var propertyType = property.PropertyType;

        // Nullable value types are not required
        if (Nullable.GetUnderlyingType(propertyType) != null)
        {
            return false;
        }

        // Value types (non-nullable) are required
        if (propertyType.IsValueType)
        {
            return true;
        }

        // Reference types - check for nullable annotations via reflection
        // Look for NullableAttribute on the property (works across .NET versions)
        var nullableAttr = property.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");

        if (nullableAttr != null && nullableAttr.ConstructorArguments.Count > 0)
        {
            var arg = nullableAttr.ConstructorArguments[0];
            if (arg.Value is byte b)
            {
                // 1 = not nullable, 2 = nullable
                return b == 1;
            }
            if (arg.Value is System.Collections.ObjectModel.ReadOnlyCollection<System.Reflection.CustomAttributeTypedArgument> bytes && bytes.Count > 0)
            {
                if (bytes[0].Value is byte firstByte)
                {
                    return firstByte == 1;
                }
            }
        }

        // Default: reference types are not required (nullable)
        return false;
    }

    private static string GetSchemaName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var backtickIndex = name.IndexOf('`');
        if (backtickIndex > 0)
        {
            name = name.Substring(0, backtickIndex);
        }

        var typeArgs = string.Join("", type.GetGenericArguments().Select(GetSchemaName));
        return name + typeArgs;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static SchemaDefinition WithNullable(SchemaDefinition schema)
    {
        if (schema.Nullable || schema.IsReference)
        {
            return schema;
        }

        return new SchemaDefinition(schema.Type!, schema.Format, true, schema.ClrType);
    }
}
