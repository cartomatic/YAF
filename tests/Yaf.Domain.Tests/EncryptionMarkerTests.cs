using AwesomeAssertions;
using Yaf.Domain.Attributes;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class EncryptAttributeTests
{
    [Fact]
    public void CanBeAppliedToStringProperty()
    {
        var attr = GetEncryptAttribute(nameof(TestEncryptableMemento.SocialSecurityNumber));

        attr.Should().NotBeNull();
    }

    [Fact]
    public void CanBeAppliedToStringArrayProperty()
    {
        var attr = GetEncryptAttribute(nameof(TestEncryptableMemento.SecretCodes));

        attr.Should().NotBeNull();
    }

    [Fact]
    public void CanBeAppliedToStringListProperty()
    {
        var attr = GetEncryptAttribute(nameof(TestEncryptableMemento.SecretNames));

        attr.Should().NotBeNull();
    }

    [Fact]
    public void CanBeAppliedToByteArrayProperty()
    {
        var attr = GetEncryptAttribute(nameof(TestEncryptableMemento.EncryptedPayload));

        attr.Should().NotBeNull();
    }

    [Fact]
    public void AttributeIsInherited()
    {
        var attr = typeof(DerivedEncryptableMemento)
            .GetProperty(nameof(DerivedEncryptableMemento.SocialSecurityNumber))!
            .GetCustomAttributes(typeof(EncryptAttribute), inherit: true);

        attr.Should().HaveCount(1);
    }

    [Fact]
    public void AttributeTargetsPropertyOnly()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(EncryptAttribute), typeof(AttributeUsageAttribute))!;

        usage.ValidOn.Should().Be(AttributeTargets.Property);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeTrue();
    }

    private static EncryptAttribute? GetEncryptAttribute(string propertyName) =>
        typeof(TestEncryptableMemento)
            .GetProperty(propertyName)!
            .GetCustomAttributes(typeof(EncryptAttribute), false)
            .OfType<EncryptAttribute>()
            .FirstOrDefault();
}

public class EncryptableInterfaceTests
{
    [Fact]
    public void IEncryptable_CanBeImplementedByMementoClass()
    {
        var memento = new TestEncryptableMemento();

        (memento is IEncryptable).Should().BeTrue();
    }

    [Fact]
    public void IEncryptable_IsMarkerInterface()
    {
        typeof(IEncryptable).GetMembers()
            .Where(m => m.DeclaringType == typeof(IEncryptable))
            .Should().BeEmpty();
    }
}

#region Test Types

internal class TestEncryptableMemento : MementoBase, IEncryptable
{
    [Encrypt]
    public string? SocialSecurityNumber { get; set; }

    [Encrypt]
    public string[]? SecretCodes { get; set; }

    [Encrypt]
    public List<string>? SecretNames { get; set; }

    [Encrypt]
    public byte[]? EncryptedPayload { get; set; }
}

internal class DerivedEncryptableMemento : TestEncryptableMemento
{
    public string? ExtraField { get; set; }
}

#endregion
