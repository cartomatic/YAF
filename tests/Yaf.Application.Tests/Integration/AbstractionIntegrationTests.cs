using AwesomeAssertions;
using Yaf.Application.Cqrs;
using Yaf.Application.Notifications;
using Yaf.Application.Sanitization;
using Yaf.Application.Validation;
using Yaf.Domain;

namespace Yaf.Application.Tests.Integration;

/// <summary>
/// Strategic cross-cutting tests that prove the Application abstractions compose
/// across namespaces (CQRS, Sanitization, Validation, Notifications) and interop
/// correctly with Domain primitives such as <see cref="Result"/> / <see cref="Result{T}"/>.
/// </summary>
public class AbstractionIntegrationTests
{
    [Fact]
    public async Task Pipeline_SanitizeThenValidateThenHandle_ProducesDomainResult()
    {
        var rawCommand = new RegisterUserCommand("  Alice@Example.COM  ");
        ISanitizer sanitizer = new TrimAndLowerSanitizer();
        IValidator<RegisterUserCommand> validator = new RegisterUserValidator();
        ICommandHandler<RegisterUserCommand, ActorId> handler = new RegisterUserHandler();

        var sanitized = sanitizer.Sanitize(rawCommand);
        var validation = await validator.ValidateAsync(sanitized, CancellationToken.None);
        var outcome = await handler.Handle(sanitized, CancellationToken.None);

        sanitized.Email.Should().Be(
            "alice@example.com",
            "Sanitize must trim and lower-case before the rest of the pipeline runs");
        validation.IsSuccess.Should().BeTrue("validator passes a sanitized email");
        outcome.IsSuccess.Should().BeTrue("handler returns Result<ActorId> success");
        outcome.Value.Should().NotBeNull("handler emits a real Domain typed-ID");
        outcome.Value.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ICommandHandler_Contravariant_DerivedHandlerAssignableToBaseReference()
    {
        ICommandHandler<DerivedCommand, int> derivedHandler = new BaseCommandHandler();

        derivedHandler.Should().NotBeNull(
            "ICommandHandler<in TCommand, TResult> contravariance permits a base-typed handler " +
            "to be assigned to a derived-typed reference");
    }

    [Fact]
    public void IQueryHandler_Contravariant_DerivedHandlerAssignableToBaseReference()
    {
        IQueryHandler<DerivedQuery, string> derivedHandler = new BaseQueryHandler();

        derivedHandler.Should().NotBeNull(
            "IQueryHandler<in TQuery, TResult> contravariance permits a base-typed handler " +
            "to be assigned to a derived-typed reference");
    }

    [Fact]
    public void NotificationAndValidator_Contravariant_DerivedReferencesAcceptBaseHandlers()
    {
        INotificationHandler<DerivedNotification> notificationHandler = new BaseNotificationHandler();
        IValidator<DerivedInstance> validator = new BaseInstanceValidator();

        notificationHandler.Should().NotBeNull(
            "INotificationHandler<in TNotification> permits base-typed handler assignment");
        validator.Should().NotBeNull(
            "IValidator<in T> permits base-typed validator assignment");
    }

    [Fact]
    public void HandlerGenericConstraints_BindCommandAndQueryToTheirTypedMarkers()
    {
        var commandHandlerTCommand = typeof(ICommandHandler<,>)
            .GetGenericArguments()
            .Single(t => t.Name == "TCommand");
        var queryHandlerTQuery = typeof(IQueryHandler<,>)
            .GetGenericArguments()
            .Single(t => t.Name == "TQuery");

        commandHandlerTCommand.GetGenericParameterConstraints()
            .Should().ContainSingle(c => c.GetGenericTypeDefinition() == typeof(ICommand<>),
                "TCommand must be constrained to ICommand<TResult>");
        queryHandlerTQuery.GetGenericParameterConstraints()
            .Should().ContainSingle(c => c.GetGenericTypeDefinition() == typeof(IQuery<>),
                "TQuery must be constrained to IQuery<TResult>");
    }

    [Fact]
    public async Task QueryHandler_ReturnsResultOfDomainTypedId_CrossLayerInteropWorks()
    {
        IQueryHandler<GetCurrentTenantQuery, TenantId> handler = new GetCurrentTenantHandler();

        var result = await handler.Handle(new GetCurrentTenantQuery(), CancellationToken.None);

        result.Should().BeOfType<Result<TenantId>>(
            "the Application query handler signature wraps Domain typed-IDs in Result<T>");
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull(
            "the handler produces a real Domain TenantId end-to-end");
        result.Value.Value.Should().NotBe(Guid.Empty);
    }
}

#region Test Types

internal sealed record RegisterUserCommand([property: Sanitize] string Email) : ICommand<ActorId>;

internal sealed class TrimAndLowerSanitizer : ISanitizer
{
    public T Sanitize<T>(T instance)
    {
        if (instance is RegisterUserCommand cmd)
        {
            object sanitized = cmd with { Email = cmd.Email.Trim().ToLowerInvariant() };
            return (T)sanitized;
        }

        return instance;
    }
}

internal sealed class RegisterUserValidator : IValidator<RegisterUserCommand>
{
    public Task<Result> ValidateAsync(RegisterUserCommand instance, CancellationToken cancellationToken) =>
        Task.FromResult(string.IsNullOrWhiteSpace(instance.Email)
            ? Result.Failure(Error.Create<RegisterUserCommand>("Email is required."))
            : Result.Success());
}

internal sealed class RegisterUserHandler : ICommandHandler<RegisterUserCommand, ActorId>
{
    public Task<Result<ActorId>> Handle(RegisterUserCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(new ActorId(Guid.NewGuid())));
}

internal record BaseCommand : ICommand<int>;
internal sealed record DerivedCommand : BaseCommand;

internal sealed class BaseCommandHandler : ICommandHandler<BaseCommand, int>
{
    public Task<Result<int>> Handle(BaseCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(1));
}

internal record BaseQuery : IQuery<string>;
internal sealed record DerivedQuery : BaseQuery;

internal sealed class BaseQueryHandler : IQueryHandler<BaseQuery, string>
{
    public Task<Result<string>> Handle(BaseQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success("ok"));
}

internal record BaseNotification : INotification;
internal sealed record DerivedNotification : BaseNotification;

internal sealed class BaseNotificationHandler : INotificationHandler<BaseNotification>
{
    public Task Handle(BaseNotification notification, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal record BaseInstance;
internal sealed record DerivedInstance : BaseInstance;

internal sealed class BaseInstanceValidator : IValidator<BaseInstance>
{
    public Task<Result> ValidateAsync(BaseInstance instance, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

internal sealed record GetCurrentTenantQuery : IQuery<TenantId>;

internal sealed class GetCurrentTenantHandler : IQueryHandler<GetCurrentTenantQuery, TenantId>
{
    public Task<Result<TenantId>> Handle(GetCurrentTenantQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(new TenantId(Guid.NewGuid())));
}

#endregion
