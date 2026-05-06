# Scope Clarifications — CQRS Abstractions

## Decisions Made

1. **Command hierarchy**: `ICommand<TResult> : ICommand` — inheritance pattern
2. **ISanitizable placement**: Application layer (overrides ADR recommendation of Domain)
3. **Namespace organization**: Sub-namespaces per concern:
   - `Yaf.Application.Cqrs` — ICommand, IQuery, handlers
   - `Yaf.Application.Notifications` — INotification, INotificationHandler
   - `Yaf.Application.Validation` — IValidator<T>
   - `Yaf.Application.Context` — context provider interfaces
   - `Yaf.Application.Sanitization` — ISanitizable, ISanitizer
   - `Yaf.Application.Audit` — IBusinessEventLog, BusinessLogEntry
4. **Context providers**: Synchronous properties (context resolved at request entry)
5. **BusinessLogEntry**: Record type with required/init properties
6. **IBusinessEventLog**: Full entry signature (context auto-populated by implementation)

## Scope
No expansion — stays within full ADR scope as agreed in Phase 1.
