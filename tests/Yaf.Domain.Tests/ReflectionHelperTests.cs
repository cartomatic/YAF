using AwesomeAssertions;
using Yaf.Domain.Helpers;

namespace Yaf.Domain.Tests;

public class ReflectionHelperTests
{
    // --- Test types ---

    private class SampleEntity
    {
        public string Name { get; private set; } = string.Empty;
        public int Count { get; private set; }
        public DateTimeOffset? Timestamp { get; private set; }
        public string? NullableRef { get; private set; }
        public string ReadOnly { get; } = "immutable";
    }

    // --- BuildPropertyWriter ---

    [Fact]
    public void BuildPropertyWriter_StringProperty_SetsValue()
    {
        var entity = new SampleEntity();

        var writer = ReflectionHelper.BuildPropertyWriter<SampleEntity>(nameof(SampleEntity.Name));
        writer(entity, "world");

        entity.Name.Should().Be("world");
    }

    [Fact]
    public void BuildPropertyWriter_ValueTypeProperty_SetsValue()
    {
        var entity = new SampleEntity();

        var writer = ReflectionHelper.BuildPropertyWriter<SampleEntity>(nameof(SampleEntity.Count));
        writer(entity, 99);

        entity.Count.Should().Be(99);
    }

    [Fact]
    public void BuildPropertyWriter_NullOnValueType_SetsDefault()
    {
        var entity = new SampleEntity();
        SetPrivateProperty(entity, nameof(SampleEntity.Count), 42);

        var writer = ReflectionHelper.BuildPropertyWriter<SampleEntity>(nameof(SampleEntity.Count));
        writer(entity, null);

        entity.Count.Should().Be(0);
    }

    [Fact]
    public void BuildPropertyWriter_NullOnReferenceType_SetsNull()
    {
        var entity = new SampleEntity();
        SetPrivateProperty(entity, nameof(SampleEntity.NullableRef), "something");

        var writer = ReflectionHelper.BuildPropertyWriter<SampleEntity>(nameof(SampleEntity.NullableRef));
        writer(entity, null);

        entity.NullableRef.Should().BeNull();
    }

    [Fact]
    public void BuildPropertyWriter_NullableValueType_SetsValue()
    {
        var entity = new SampleEntity();
        var now = DateTimeOffset.UtcNow;

        var writer = ReflectionHelper.BuildPropertyWriter<SampleEntity>(nameof(SampleEntity.Timestamp));
        writer(entity, now);

        entity.Timestamp.Should().Be(now);
    }

    [Fact]
    public void BuildPropertyWriter_ReadOnlyProperty_Throws()
    {
        var action = () => ReflectionHelper.BuildPropertyWriter<SampleEntity>(nameof(SampleEntity.ReadOnly));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*must have a setter*");
    }

    [Fact]
    public void BuildPropertyWriter_MissingProperty_Throws()
    {
        var action = () => ReflectionHelper.BuildPropertyWriter<SampleEntity>("NonExistent");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing*'NonExistent'*");
    }

    // --- Helper ---

    private static void SetPrivateProperty<T>(T entity, string propertyName, object? value) =>
        typeof(T).GetProperty(propertyName)!.GetSetMethod(nonPublic: true)!.Invoke(entity, [value]);
}
