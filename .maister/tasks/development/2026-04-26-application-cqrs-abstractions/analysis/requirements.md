# Requirements — CQRS Abstractions

## Task Description
Add full application layer abstractions to Yaf.Application per ADRs 20260324-1144 and 20260324-1146.

## Scope (from clarifications)
- CQRS: ICommand, ICommand<TResult>, IQuery<TResult>, ICommandHandler variants, IQueryHandler
- Notifications: INotification, INotificationHandler<T>
- Validation: IValidator<T>
- Context providers: ITenantContextProvider, IIdentityContextProvider, ICorrelationIdProvider, IActivityIdProvider
- Sanitization: ISanitizable, ISanitizer
- Business event log: IBusinessEventLog, BusinessLogEntry

## Design Decisions
1. **Command hierarchy**: `ICommand<TResult> : ICommand` (inheritance)
2. **ISanitizable**: in Application layer (user override of ADR Domain placement)
3. **Namespaces**: Sub-namespaces per concern (Cqrs, Notifications, Validation, Context, Sanitization, Audit)
4. **Context providers**: Synchronous properties
5. **BusinessLogEntry**: Record type
6. **Handler methods**: Async only — `Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken)`
7. **IValidator<T>**: Returns `Result` from Yaf.Domain

## Functional Requirements
1. All interfaces are marker or thin abstractions — no implementation code
2. Handlers return `Result` (void commands) or `Result<TResult>` (typed)
3. All public APIs must have XML documentation (CS1591 enforced)
4. Follow coding standards: file-scoped namespaces, semicolon body markers, generic filenames with braces
5. One interface per file
6. No external NuGet dependencies — only reference Yaf.Domain

## Reusability
- Domain types used directly: `Result`, `Result<T>`, `Error`, `TenantId`, `ActorId`
- Pattern templates: IDomainEvent.cs (interface style), IMemento.cs (CRTP pattern)

## Scope Boundaries
- NO adapter implementations (Wolverine adapter is a separate task)
- NO pipeline behavior implementations (infrastructure concern)
- NO concrete context provider implementations
- Interfaces and records only
