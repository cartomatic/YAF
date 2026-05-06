using System.Reflection;
using AwesomeAssertions;
using Yaf.Application.Validation;
using Yaf.Domain;

namespace Yaf.Application.Tests.Validation;

public class ValidatorContractTests
{
    [Fact]
    public async Task IValidator_ValidateAsyncMethod_ReturnsTaskOfResult()
    {
        IValidator<SampleInstance> validator = new SampleValidator();

        var result = await validator.ValidateAsync(new SampleInstance(), CancellationToken.None);

        result.Should().BeOfType<Result>();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void IValidator_T_IsContravariant()
    {
        var typeParameter = typeof(IValidator<>)
            .GetGenericArguments()
            .Single(t => t.Name == "T");

        typeParameter.GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.Contravariant)
            .Should().BeTrue();
    }

    [Fact]
    public void IValidator_ValidateAsync_AcceptsCancellationToken()
    {
        var method = typeof(IValidator<SampleInstance>)
            .GetMethod(nameof(IValidator<SampleInstance>.ValidateAsync))!;

        method.ReturnType.Should().Be(typeof(Task<Result>));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(SampleInstance));
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken));
    }
}

#region Test Types

internal sealed record SampleInstance;

internal sealed class SampleValidator : IValidator<SampleInstance>
{
    public Task<Result> ValidateAsync(SampleInstance instance, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

#endregion
