# Clarifications — CQRS Abstractions

## Scope Decision
**Full ADR scope** — implement all abstractions from ADRs 20260324-1144 and 20260324-1146:
- CQRS: ICommand, ICommand<TResult>, IQuery<TResult>, ICommandHandler, IQueryHandler
- Notifications: INotification, INotificationHandler<T>
- Validation: IValidator<T>
- Context providers: ITenantContextProvider, IIdentityContextProvider, ICorrelationIdProvider, IActivityIdProvider
- Sanitization: ISanitizable, ISanitizer
- Business event log: IBusinessEventLog

## Command Design Decision
**Both ICommand and ICommand<TResult>** — two-level hierarchy:
- Non-generic `ICommand` for void commands (returns `Result`)
- Generic `ICommand<TResult>` for typed responses (returns `Result<TResult>`)
- Same pattern applies to queries: `IQuery<TResult>` (queries always return data)
- Handlers mirror: `ICommandHandler<TCommand>` and `ICommandHandler<TCommand, TResult>`
