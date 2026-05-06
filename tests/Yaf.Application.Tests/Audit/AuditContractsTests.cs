using System.Reflection;
using AwesomeAssertions;
using Yaf.Application.Audit;

namespace Yaf.Application.Tests.Audit;

public class AuditContractsTests
{
    [Fact]
    public void BusinessLogEntry_IsRecord_HasValueSemantics()
    {
        // The compiler emits an `EqualityContract` property on every record type;
        // its presence is the canonical reflection-only signal that the type was
        // declared with the `record` keyword and therefore carries value semantics.
        var equalityContract = typeof(BusinessLogEntry).GetProperty(
            "EqualityContract",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        equalityContract.Should().NotBeNull(
            "BusinessLogEntry must be declared as a record so audit log entries compare by value");

        typeof(BusinessLogEntry).IsSealed.Should().BeTrue(
            "BusinessLogEntry is a leaf DTO and must be sealed to forbid behavior-bearing subclasses");
    }

    [Fact]
    public void BusinessLogEntry_HasNineRequiredProperties_MatchingSpec()
    {
        var expected = new (string Name, Type Type, bool IsRequired)[]
        {
            ("What", typeof(string), true),
            ("AggregateType", typeof(string), true),
            ("AggregateId", typeof(Guid), true),
            ("TenantId", typeof(Guid?), true),
            ("ActorId", typeof(Guid), true),
            ("CorrelationId", typeof(Guid), true),
            ("ActivityId", typeof(string), false),
            ("OccurredAtUtc", typeof(DateTimeOffset), true),
            ("Metadata", typeof(IReadOnlyDictionary<string, object>), false),
        };

        var properties = typeof(BusinessLogEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        properties.Should().HaveCount(
            expected.Length,
            "spec defines exactly nine business log entry properties");

        foreach (var (name, type, isRequired) in expected)
        {
            var property = properties.SingleOrDefault(p => p.Name == name);
            property.Should().NotBeNull($"BusinessLogEntry must declare {name}");

            property!.PropertyType.Should().Be(
                type,
                $"{name} must be typed as {type.Name}");

            var hasRequiredAttribute = property.GetCustomAttributes()
                .Any(a => a.GetType().FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute");

            hasRequiredAttribute.Should().Be(
                isRequired,
                $"{name} required-modifier expectation does not match spec");
        }
    }

    [Fact]
    public void BusinessLogEntry_TenantIdProperty_IsNullableGuid()
    {
        var property = typeof(BusinessLogEntry).GetProperty(nameof(BusinessLogEntry.TenantId))!;

        property.PropertyType.Should().Be(typeof(Guid?));
        Nullable.GetUnderlyingType(property.PropertyType).Should().Be(
            typeof(Guid),
            "TenantId is Guid? (primitive) — log entries are DTOs and avoid domain-typed IDs to skip reconstruction on read-back");
    }

    [Fact]
    public void BusinessLogEntry_ActivityIdProperty_IsNullableString()
    {
        var property = typeof(BusinessLogEntry).GetProperty(nameof(BusinessLogEntry.ActivityId))!;

        property.PropertyType.Should().Be(
            typeof(string),
            "ActivityId is string? — drift from ADR-1146 which used Guid; W3C trace identifiers are formatted strings, not GUIDs");

        var info = new NullabilityInfoContext().Create(property);
        info.ReadState.Should().Be(
            NullabilityState.Nullable,
            "ActivityId is nullable when no System.Diagnostics.Activity is active");
    }

    [Fact]
    public void BusinessLogEntry_MetadataProperty_IsNullableReadOnlyDictionary()
    {
        var property = typeof(BusinessLogEntry).GetProperty(nameof(BusinessLogEntry.Metadata))!;

        property.PropertyType.Should().Be(
            typeof(IReadOnlyDictionary<string, object>),
            "Metadata is exposed as IReadOnlyDictionary<string, object> — read-only DTO surface");

        var info = new NullabilityInfoContext().Create(property);
        info.ReadState.Should().Be(
            NullabilityState.Nullable,
            "Metadata is optional — entries without contextual data omit the dictionary entirely");
    }

    [Fact]
    public void IBusinessEventLog_AppendAsyncMethod_AcceptsExpectedParameters()
    {
        var method = typeof(IBusinessEventLog).GetMethod(nameof(IBusinessEventLog.AppendAsync))!;

        method.ReturnType.Should().Be(
            typeof(Task),
            "AppendAsync returns non-generic Task — failures surface via thrown exceptions, not Result<T>");

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(
            5,
            "AppendAsync takes four log inputs plus the cancellation token");

        parameters[0].Name.Should().Be("what");
        parameters[0].ParameterType.Should().Be(typeof(string));

        parameters[1].Name.Should().Be("aggregateType");
        parameters[1].ParameterType.Should().Be(typeof(string));

        parameters[2].Name.Should().Be("aggregateId");
        parameters[2].ParameterType.Should().Be(typeof(Guid));

        parameters[3].Name.Should().Be("metadata");
        parameters[3].ParameterType.Should().Be(typeof(IReadOnlyDictionary<string, object>));
        new NullabilityInfoContext().Create(parameters[3]).ReadState.Should().Be(
            NullabilityState.Nullable,
            "metadata is optional — implementations accept null when no contextual data is supplied");

        parameters[4].Name.Should().Be("cancellationToken");
        parameters[4].ParameterType.Should().Be(typeof(CancellationToken));
    }
}
