using System.Reflection;
using AwesomeAssertions;
using Yaf.Application.Cqrs;
using Yaf.Domain;

namespace Yaf.Application.Tests.Cqrs;

public class CqrsContractsTests
{
    [Fact]
    public void ICommandGeneric_InheritsFromICommand_AssignableToBaseInterface()
    {
        var commandWithResult = new SampleCommandWithResult();

        (commandWithResult is ICommand).Should().BeTrue();
        typeof(ICommand).IsAssignableFrom(typeof(ICommand<int>)).Should().BeTrue();
    }

    [Fact]
    public void ICommandGeneric_IsCovariant_AssignableToBaseTypeParameter()
    {
        ICommand<Derived> derivedCommand = new SampleCommandReturningDerived();

        ICommand<Base> covariant = derivedCommand;

        covariant.Should().BeSameAs(derivedCommand);
    }

    [Fact]
    public async Task ICommandHandler_HandleMethod_ReturnsTaskOfResult()
    {
        ICommandHandler<SampleCommand> handler = new SampleCommandHandler();

        var result = await handler.Handle(new SampleCommand(), CancellationToken.None);

        result.Should().BeOfType<Result>();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ICommandHandlerGeneric_HandleMethod_ReturnsTaskOfResultOfTResult()
    {
        ICommandHandler<SampleCommandWithResult, int> handler = new SampleCommandWithResultHandler();

        var result = await handler.Handle(new SampleCommandWithResult(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task IQueryHandler_HandleMethod_ReturnsTaskOfResultOfTResult()
    {
        IQueryHandler<SampleQuery, string> handler = new SampleQueryHandler();

        var result = await handler.Handle(new SampleQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
    }

    [Fact]
    public void CqrsHandlers_GenericParameters_HaveExpectedVarianceAnnotations()
    {
        GetGenericParameter(typeof(ICommandHandler<>), "TCommand")
            .GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.Contravariant)
            .Should().BeTrue();

        GetGenericParameter(typeof(ICommandHandler<,>), "TCommand")
            .GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.Contravariant)
            .Should().BeTrue();

        GetGenericParameter(typeof(IQueryHandler<,>), "TQuery")
            .GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.Contravariant)
            .Should().BeTrue();

        GetGenericParameter(typeof(ICommand<>), "TResult")
            .GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.Covariant)
            .Should().BeTrue();

        GetGenericParameter(typeof(IQuery<>), "TResult")
            .GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.Covariant)
            .Should().BeTrue();
    }

    private static Type GetGenericParameter(Type genericTypeDefinition, string name) =>
        genericTypeDefinition.GetGenericArguments().Single(t => t.Name == name);
}

#region Test Types

internal record Base;
internal record Derived : Base;

internal sealed record SampleCommand : ICommand;

internal sealed record SampleCommandWithResult : ICommand<int>;

internal sealed record SampleCommandReturningDerived : ICommand<Derived>;

internal sealed record SampleQuery : IQuery<string>;

internal sealed class SampleCommandHandler : ICommandHandler<SampleCommand>
{
    public Task<Result> Handle(SampleCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

internal sealed class SampleCommandWithResultHandler : ICommandHandler<SampleCommandWithResult, int>
{
    public Task<Result<int>> Handle(SampleCommandWithResult command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(42));
}

internal sealed class SampleQueryHandler : IQueryHandler<SampleQuery, string>
{
    public Task<Result<string>> Handle(SampleQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success("ok"));
}

#endregion
