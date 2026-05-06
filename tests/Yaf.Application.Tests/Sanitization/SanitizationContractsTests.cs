using System.Reflection;
using AwesomeAssertions;
using Yaf.Application.Sanitization;

namespace Yaf.Application.Tests.Sanitization;

public class SanitizationContractsTests
{
    [Fact]
    public void SanitizeAttribute_AllowsPropertyAndTypeTargets()
    {
        var usage = typeof(SanitizeAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>()!;

        usage.ValidOn.Should().HaveFlag(AttributeTargets.Property);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Struct);
        usage.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public void SanitizeAttribute_IsSealed()
    {
        typeof(SanitizeAttribute).IsSealed.Should().BeTrue(
            "the attribute carries no implementation surface and should not be extended");
    }

    [Fact]
    public void ISanitizer_SanitizeMethod_IsGenericAndUnconstrained()
    {
        var method = typeof(ISanitizer)
            .GetMethod(nameof(ISanitizer.Sanitize))!;

        method.IsGenericMethodDefinition.Should().BeTrue();

        var typeParameter = method.GetGenericArguments().Single();
        typeParameter.GetGenericParameterConstraints().Should().BeEmpty(
            "the attribute model removes the marker-interface constraint; opt-in is via [Sanitize]");
    }

    [Fact]
    public void ISanitizer_SanitizeMethod_IsSynchronousReturnsT()
    {
        var method = typeof(ISanitizer)
            .GetMethod(nameof(ISanitizer.Sanitize))!;

        var typeParameter = method.GetGenericArguments().Single();

        method.ReturnType.Should().Be(
            typeParameter,
            "Sanitize is a synchronous in-memory transformation that returns a sanitized copy of T (not Task<T>)");

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeParameter);
    }

    [Fact]
    public void SanitizeAttribute_AppliedToProperty_IsDiscoverableByReflection()
    {
        var property = typeof(SamplePerPropertyOptIn).GetProperty(nameof(SamplePerPropertyOptIn.Email))!;
        property.GetCustomAttribute<SanitizeAttribute>().Should().NotBeNull();

        var unmarked = typeof(SamplePerPropertyOptIn).GetProperty(nameof(SamplePerPropertyOptIn.DisplayName))!;
        unmarked.GetCustomAttribute<SanitizeAttribute>().Should().BeNull();
    }

    [Fact]
    public void SanitizeAttribute_AppliedToType_IsDiscoverableByReflection() =>
        typeof(SamplePerTypeOptIn).GetCustomAttribute<SanitizeAttribute>().Should().NotBeNull();

    #region Test Types

    private sealed record SamplePerPropertyOptIn(
        [property: Sanitize] string Email,
        string DisplayName);

    [Sanitize]
    private sealed record SamplePerTypeOptIn(string Title, string Body);

    #endregion
}
