using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class TypedIdTests
{
    private record TestGuidId(Guid Value) : TypedId(Value);
    private record AnotherGuidId(Guid Value) : TypedId(Value);

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

        (id is ITypedId).Should().BeTrue();
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
