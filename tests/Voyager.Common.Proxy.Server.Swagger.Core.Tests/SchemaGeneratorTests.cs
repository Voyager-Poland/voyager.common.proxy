using Voyager.Common.Proxy.Server.Swagger.Core;

namespace Voyager.Common.Proxy.Server.Swagger.Core.Tests;

public class SchemaGeneratorTests
{
    private readonly SchemaGenerator _generator = new();

    #region Test Types

    public class SimpleClass
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class NullablePropertiesClass
    {
        public int? OptionalInt { get; set; }
        public string? OptionalString { get; set; }
        public DateTime? OptionalDate { get; set; }
    }

    public class NestedClass
    {
        public int Id { get; set; }
        public SimpleClass? Child { get; set; }
    }

    public class CollectionClass
    {
        public List<string> Tags { get; set; } = new();
        public IEnumerable<int> Numbers { get; set; } = Array.Empty<int>();
        public SimpleClass[] Items { get; set; } = Array.Empty<SimpleClass>();
    }

    public enum Status
    {
        Pending,
        Active,
        Completed
    }

    public class EnumPropertyClass
    {
        public Status Status { get; set; }
        public Status? OptionalStatus { get; set; }
    }

    #endregion

    #region Primitive Types Tests

    [Theory]
    [InlineData(typeof(int), "integer", "int32")]
    [InlineData(typeof(long), "integer", "int64")]
    [InlineData(typeof(short), "integer", "int32")]
    [InlineData(typeof(byte), "integer", "int32")]
    [InlineData(typeof(float), "number", "float")]
    [InlineData(typeof(double), "number", "double")]
    [InlineData(typeof(decimal), "number", "double")]
    [InlineData(typeof(bool), "boolean", null)]
    [InlineData(typeof(string), "string", null)]
    [InlineData(typeof(DateTime), "string", "date-time")]
    [InlineData(typeof(DateTimeOffset), "string", "date-time")]
    [InlineData(typeof(Guid), "string", "uuid")]
    [InlineData(typeof(TimeSpan), "string", "duration")]
    public void GenerateSchema_PrimitiveTypes_ReturnsCorrectTypeAndFormat(Type type, string expectedType, string? expectedFormat)
    {
        var schema = _generator.GenerateSchema(type);

        Assert.Equal(expectedType, schema.Type);
        Assert.Equal(expectedFormat, schema.Format);
        Assert.False(schema.IsReference);
    }

    [Theory]
    [InlineData(typeof(int?))]
    [InlineData(typeof(bool?))]
    [InlineData(typeof(DateTime?))]
    [InlineData(typeof(Guid?))]
    public void GenerateSchema_NullablePrimitives_SetsNullableTrue(Type type)
    {
        var schema = _generator.GenerateSchema(type);

        Assert.True(schema.Nullable);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(DateTime))]
    public void GenerateSchema_NonNullablePrimitives_SetsNullableFalse(Type type)
    {
        var schema = _generator.GenerateSchema(type);

        Assert.False(schema.Nullable);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void GenerateSchema_Enum_ReturnsStringWithEnumValues()
    {
        var schema = _generator.GenerateSchema(typeof(Status));

        Assert.Equal("string", schema.Type);
        Assert.NotNull(schema.EnumValues);
        Assert.Contains("Pending", schema.EnumValues);
        Assert.Contains("Active", schema.EnumValues);
        Assert.Contains("Completed", schema.EnumValues);
    }

    [Fact]
    public void GenerateSchema_NullableEnum_SetsNullableTrue()
    {
        var schema = _generator.GenerateSchema(typeof(Status?));

        Assert.True(schema.Nullable);
        Assert.NotNull(schema.EnumValues);
    }

    #endregion

    #region Array and Collection Tests

    [Fact]
    public void GenerateSchema_Array_ReturnsArrayTypeWithItems()
    {
        var schema = _generator.GenerateSchema(typeof(int[]));

        Assert.Equal("array", schema.Type);
        Assert.NotNull(schema.Items);
        Assert.Equal("integer", schema.Items.Type);
    }

    [Fact]
    public void GenerateSchema_List_ReturnsArrayTypeWithItems()
    {
        var schema = _generator.GenerateSchema(typeof(List<string>));

        Assert.Equal("array", schema.Type);
        Assert.NotNull(schema.Items);
        Assert.Equal("string", schema.Items.Type);
    }

    [Fact]
    public void GenerateSchema_IEnumerable_ReturnsArrayTypeWithItems()
    {
        var schema = _generator.GenerateSchema(typeof(IEnumerable<int>));

        Assert.Equal("array", schema.Type);
        Assert.NotNull(schema.Items);
        Assert.Equal("integer", schema.Items.Type);
    }

    [Fact]
    public void GenerateSchema_ArrayOfComplexType_ReturnsArrayWithReference()
    {
        var schema = _generator.GenerateSchema(typeof(SimpleClass[]));

        Assert.Equal("array", schema.Type);
        Assert.NotNull(schema.Items);
        Assert.True(schema.Items.IsReference);
    }

    #endregion

    #region Complex Type Tests

    [Fact]
    public void GenerateSchema_ComplexType_ReturnsReference()
    {
        var schema = _generator.GenerateSchema(typeof(SimpleClass));

        Assert.True(schema.IsReference);
        Assert.Contains("SimpleClass", schema.Reference);
    }

    [Fact]
    public void GenerateSchema_ComplexType_AddsToComponentSchemas()
    {
        _generator.GenerateSchema(typeof(SimpleClass));

        Assert.True(_generator.ComponentSchemas.ContainsKey("SimpleClass"));
        var componentSchema = _generator.ComponentSchemas["SimpleClass"];
        Assert.NotNull(componentSchema.Properties);
        Assert.True(componentSchema.Properties.ContainsKey("id"));
        Assert.True(componentSchema.Properties.ContainsKey("name"));
        Assert.True(componentSchema.Properties.ContainsKey("isActive"));
    }

    [Fact]
    public void GenerateSchema_ComplexType_PropertyNamesAreCamelCase()
    {
        _generator.GenerateSchema(typeof(SimpleClass));

        var componentSchema = _generator.ComponentSchemas["SimpleClass"];
        Assert.True(componentSchema.Properties!.ContainsKey("id"));
        Assert.True(componentSchema.Properties.ContainsKey("name"));
        Assert.True(componentSchema.Properties.ContainsKey("isActive"));
        Assert.False(componentSchema.Properties.ContainsKey("Id"));
        Assert.False(componentSchema.Properties.ContainsKey("Name"));
        Assert.False(componentSchema.Properties.ContainsKey("IsActive"));
    }

    [Fact]
    public void GenerateSchema_ComplexType_SetsRequiredForNonNullableValueTypes()
    {
        _generator.GenerateSchema(typeof(SimpleClass));

        var componentSchema = _generator.ComponentSchemas["SimpleClass"];
        Assert.NotNull(componentSchema.Required);
        Assert.Contains("id", componentSchema.Required);
        Assert.Contains("isActive", componentSchema.Required);
    }

    [Fact]
    public void GenerateSchema_NestedComplexType_GeneratesBothSchemas()
    {
        _generator.GenerateSchema(typeof(NestedClass));

        Assert.True(_generator.ComponentSchemas.ContainsKey("NestedClass"));
        Assert.True(_generator.ComponentSchemas.ContainsKey("SimpleClass"));
    }

    [Fact]
    public void GenerateSchema_SameTypeTwice_GeneratesOnlyOnce()
    {
        _generator.GenerateSchema(typeof(SimpleClass));
        var count1 = _generator.ComponentSchemas.Count;

        _generator.GenerateSchema(typeof(SimpleClass));
        var count2 = _generator.ComponentSchemas.Count;

        Assert.Equal(count1, count2);
    }

    #endregion

    #region Dictionary Tests

    [Fact]
    public void GenerateSchema_DictionaryStringString_TreatedAsArray()
    {
        // Current implementation treats dictionaries as IEnumerable<KeyValuePair>
        var schema = _generator.GenerateSchema(typeof(Dictionary<string, string>));

        Assert.Equal("array", schema.Type);
    }

    [Fact]
    public void GenerateSchema_DictionaryStringInt_TreatedAsArray()
    {
        // Current implementation treats dictionaries as IEnumerable<KeyValuePair>
        var schema = _generator.GenerateSchema(typeof(Dictionary<string, int>));

        Assert.Equal("array", schema.Type);
    }

    #endregion

    #region ClrType Preservation Tests

    [Fact]
    public void GenerateSchema_ComplexType_PreservesClrType()
    {
        _generator.GenerateSchema(typeof(SimpleClass));

        var componentSchema = _generator.ComponentSchemas["SimpleClass"];
        Assert.Equal(typeof(SimpleClass), componentSchema.ClrType);
    }

    [Fact]
    public void GenerateSchema_PrimitiveType_PreservesClrType()
    {
        var schema = _generator.GenerateSchema(typeof(int));

        Assert.Equal(typeof(int), schema.ClrType);
    }

    #endregion
}
