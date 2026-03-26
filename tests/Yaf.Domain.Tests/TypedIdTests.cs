using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class TypedIdTests
{
    private record TestGuidId(Guid Value) : TypedId<Guid>(Value);
    private record AnotherGuidId(Guid Value) : TypedId<Guid>(Value);
    private record TestIntId(int Value) : TypedId<int>(Value);
    private record TestLongId(long Value) : TypedId<long>(Value);
    private record TestStringId(string Value) : TypedId<string>(Value);

    [Fact]
    public void Equality_WithSameTypeAndValue_ReturnsTrue()
    {
        var guid = Guid.NewGuid();
        var id1 = new TestGuidId(guid);
        var id2 = new TestGuidId(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentValues_ReturnsFalse()
    {
        var id1 = new TestGuidId(Guid.NewGuid());
        var id2 = new TestGuidId(Guid.NewGuid());

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentTypedIdTypes_WithSameValue_ReturnsFalse()
    {
        var guid = Guid.NewGuid();
        var orderId = new TestGuidId(guid);
        var customerId = new AnotherGuidId(guid);

        orderId.Equals(customerId).Should().BeFalse();
    }

    [Fact]
    public void Value_ReturnsBackingValue()
    {
        var guid = Guid.NewGuid();
        var id = new TestGuidId(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ToString_ReturnsReadableRepresentation()
    {
        var guid = Guid.NewGuid();
        var id = new TestGuidId(guid);

        id.ToString().Should().Contain(guid.ToString());
    }

    [Fact]
    public void TypeHierarchy_ImplementsITypedId()
    {
        var id = new TestGuidId(Guid.NewGuid());

        id.Should().BeAssignableTo<ITypedId>();
    }

    [Fact]
    public void TypeHierarchy_ImplementsGenericITypedId()
    {
        var id = new TestGuidId(Guid.NewGuid());

        (id is ITypedId<Guid>).Should().BeTrue();
    }

    [Fact]
    public void IntBackingType_EqualityWorks()
    {
        var id1 = new TestIntId(42);
        var id2 = new TestIntId(42);
        var id3 = new TestIntId(99);

        id1.Should().Be(id2);
        id1.Should().NotBe(id3);
    }

    [Fact]
    public void LongBackingType_EqualityWorks()
    {
        var id1 = new TestLongId(123456789L);
        var id2 = new TestLongId(123456789L);
        var id3 = new TestLongId(987654321L);

        id1.Should().Be(id2);
        id1.Should().NotBe(id3);
    }

    [Fact]
    public void StringBackingType_EqualityWorks()
    {
        var id1 = new TestStringId("abc");
        var id2 = new TestStringId("abc");
        var id3 = new TestStringId("xyz");

        id1.Should().Be(id2);
        id1.Should().NotBe(id3);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        var guid = Guid.NewGuid();
        var id1 = new TestGuidId(guid);
        var id2 = new TestGuidId(guid);

        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentTypes_SameValue_ReturnsDifferentHash()
    {
        var guid = Guid.NewGuid();
        var id1 = new TestGuidId(guid);
        var id2 = new AnotherGuidId(guid);

        id1.GetHashCode().Should().NotBe(id2.GetHashCode());
    }
}
