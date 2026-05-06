using System.Reflection;
using AwesomeAssertions;
using Yaf.Application.Context;
using Yaf.Domain;

namespace Yaf.Application.Tests.Context;

public class ContextProviderContractsTests
{
    [Fact]
    public void ITenantContextProvider_TenantIdProperty_ReturnsNullableTenantId()
    {
        var property = typeof(ITenantContextProvider).GetProperty(nameof(ITenantContextProvider.TenantId))!;

        property.PropertyType.Should().Be(typeof(TenantId));
        IsNullableReferenceProperty(property).Should().BeTrue(
            "ITenantContextProvider.TenantId is documented as nullable to support system operations");
    }

    [Fact]
    public void IIdentityContextProvider_ActorIdProperty_ReturnsActorId()
    {
        var property = typeof(IIdentityContextProvider).GetProperty(nameof(IIdentityContextProvider.ActorId))!;

        property.PropertyType.Should().Be(typeof(ActorId));
        IsNullableReferenceProperty(property).Should().BeFalse(
            "IIdentityContextProvider.ActorId is non-nullable; infrastructure provides a system actor when no human is present");
    }

    [Fact]
    public void ICorrelationIdProvider_CorrelationIdProperty_ReturnsGuid()
    {
        var property = typeof(ICorrelationIdProvider).GetProperty(nameof(ICorrelationIdProvider.CorrelationId))!;

        property.PropertyType.Should().Be(typeof(Guid));
    }

    [Fact]
    public void IActivityIdProvider_ActivityIdProperty_ReturnsNullableString()
    {
        var property = typeof(IActivityIdProvider).GetProperty(nameof(IActivityIdProvider.ActivityId))!;

        property.PropertyType.Should().Be(typeof(string));
        IsNullableReferenceProperty(property).Should().BeTrue(
            "IActivityIdProvider.ActivityId is nullable when no System.Diagnostics.Activity is active");
    }

    [Fact]
    public void ContextProviders_AllPropertiesAreReadOnly()
    {
        var providers = new[]
        {
            typeof(ITenantContextProvider),
            typeof(IIdentityContextProvider),
            typeof(ICorrelationIdProvider),
            typeof(IActivityIdProvider),
        };

        foreach (var providerType in providers)
        {
            foreach (var property in providerType.GetProperties())
            {
                property.CanWrite.Should().BeFalse(
                    $"{providerType.Name}.{property.Name} must be read-only — context providers expose ambient state, not setters");
                property.SetMethod.Should().BeNull(
                    $"{providerType.Name}.{property.Name} must not declare a setter");
            }
        }
    }

    private static bool IsNullableReferenceProperty(PropertyInfo property)
    {
        if (property.PropertyType.IsValueType)
        {
            return Nullable.GetUnderlyingType(property.PropertyType) is not null;
        }

        var context = new NullabilityInfoContext();
        var info = context.Create(property);
        return info.ReadState == NullabilityState.Nullable;
    }
}
