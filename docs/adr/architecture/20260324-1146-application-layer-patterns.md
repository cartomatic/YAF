# Application Layer Patterns

- **Timestamp:** 2026-03-24 11:46
- **Status:** accepted
- **Scope:** architecture
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

The Application layer (Yaf.Application) orchestrates domain operations without containing business logic itself. Beyond CQRS handlers (covered in a separate ADR), it owns three key concerns: context providers (tenant, identity, correlation, activity), input sanitization, and the business event log. These are defined as interfaces in Application and implemented in Infrastructure.

## Drivers

1. **Traceability** — Every operation needs to carry who, which tenant, and a correlation chain. These context values feed into domain events, business logs, accountability fields, and observability.
2. **Security at the boundary** — User input must be sanitized before it reaches domain logic. XSS, HTML injection, and similar threats should be neutralized at the application boundary.
3. **Audit and compliance** — Business processes need a human-readable audit trail beyond technical logs. "Order placed by user X for tenant Y" is a business event, not an infrastructure log line.
4. **Interface ownership** — Application defines what context it needs. Infrastructure provides it. This follows the dependency rule — inner layers define contracts, outer layers implement.

## Options

### Context Providers

| Option | Assessment |
|--------|------------|
| **Dedicated provider interfaces per concern** | **Selected.** `ITenantContextProvider`, `IIdentityContextProvider`, `ICorrelationIdProvider`, `IActivityIdProvider` — each focused, independently injectable, independently testable. |
| Single `IOperationContext` aggregate | Bundles everything into one object. Harder to test individually, harder to evolve (adding a new context field affects all consumers). |
| HttpContext access throughout | Couples application logic to ASP.NET Core. Breaks testability and the dependency rule. |

### Sanitization

| Option | Assessment |
|--------|------------|
| **`[Sanitize]` attribute + pipeline sanitization** | **Selected.** Application defines a `[Sanitize]` attribute that targets individual properties or whole types. The pipeline sanitizer reflects on the instance and applies the configured rules to opted-in members. Lives in `Yaf.Application.Sanitization` — sanitization is a pipeline concern, not a domain concern. |
| `ISanitizable` marker interface | Earlier draft. Type-level only — cannot opt in individual fields. The attribute form expresses sanitization at the field level when desired and composes with class-level opt-in for the common "sanitize all strings" case. |
| Manual sanitization in handlers | Repetitive, easy to forget. One missed handler is a vulnerability. |
| Sanitization in the API layer only | API can catch HTML in request DTOs, but if commands are constructed from other sources (events, background jobs), those inputs are unsanitized. Application boundary is safer. |

### Business Event Log

| Option | Assessment |
|--------|------------|
| **`IBusinessEventLog` in Application, append-only** | **Selected.** Human-readable log entries for audit and compliance. Not reactive — no handlers triggered. Separate from domain events. |
| Reuse domain events for audit | Domain events are technical signals, not human-readable audit entries. Mixing purposes makes both worse. |
| Infrastructure-level audit log (EF Core interceptor) | Captures data changes, not business intent. "Column X changed from A to B" is not "Customer upgraded to VIP." |

## Recommendation

Dedicated context provider interfaces, pipeline sanitization with the `[Sanitize]` attribute, and an append-only business event log. All defined in Application, implemented in Infrastructure.

## Consequences

**Positive:**
- Context is available everywhere it's needed (events, audit, accountability) through a clean injection model
- Sanitization is automatic and comprehensive — applied at the pipeline level, not per-handler
- Business event log captures intent, not just data changes — valuable for compliance and customer support
- All three concerns follow the dependency rule — Application defines, Infrastructure implements

**Negative:**
- Four separate context providers means four DI registrations and four constructor parameters where all are needed (mitigated: most code only needs one or two)
- Pipeline sanitization adds a processing step to every command (mitigated: only processes properties marked with `[Sanitize]` or types whose declaration carries the attribute)
- Business event log is another thing to persist — storage and query infrastructure needed

## Conclusion

### Context Providers

| Provider | Interface (Application) | Purpose | Populated From (Infrastructure) |
|----------|------------------------|---------|--------------------------------|
| **Tenant** | `ITenantContextProvider` | Which tenant the operation is scoped to | HTTP header, subdomain, JWT claim |
| **Identity** | `IIdentityContextProvider` | Who is performing the operation | JWT claims, auth middleware |
| **Correlation** | `ICorrelationIdProvider` | Cross-service request correlation | HTTP header (`X-Correlation-Id`), generated if absent |
| **Activity** | `IActivityIdProvider` | Within-application activity tracking | Generated per operation, propagated through the chain |

**Usage pattern:**
- Context providers are injected where needed (handlers, pipeline behaviors, infrastructure services)
- Domain event dispatch reads from providers to populate the context envelope
- Accountability auto-population reads from `IIdentityContextProvider`
- Query filters read from `ITenantContextProvider`

**Propagation:** Context values flow from the API adapter (where they're resolved from the HTTP request) through the application pipeline into domain events, business log entries, and infrastructure auto-population.

**Cross-service context restoration:** Context must survive service-to-service hops. When Service A publishes an integration event, the context envelope (tenant, identity, correlation, activity) is serialized into message headers. When Service B receives the event, its infrastructure restores the context providers from those headers — so the entire downstream chain (event handlers, business log entries, further integration events, replies) carries the original context. This means context providers must support three initialization modes:
- **From HTTP request** — API adapter resolves from headers, JWT claims, etc. (direct client call)
- **From message headers** — messaging infrastructure restores from the inbound message envelope (integration events)
- **From service-to-service HTTP call** — calling service propagates context via HTTP headers; receiving service's API adapter restores context from those headers, same as a direct client request

On the outbound side, infrastructure provides delegating handlers (for `HttpClient`) and message header enrichers that automatically attach the current context to outgoing HTTP requests and messages. Consumers don't manually propagate context — it happens transparently.

The correlation ID stays the same across the entire chain. Activity IDs may fork (new activity per service) while preserving the parent activity for traceability.

### Sanitization

| Concept | Layer | Description |
|---------|-------|-------------|
| `[Sanitize]` attribute | Application | Marks individual properties or whole types as opted in to sanitization. When applied to a type, every supported string-shaped property (`string`, `string[]`, `List<string>`, `IList<string>`, `IReadOnlyList<string>`) is sanitized automatically. |
| `ISanitizer` | Application | Interface for sanitization logic. Single method `T Sanitize<T>(T instance)` — synchronous, returns a sanitized copy (record-friendly), no marker-interface constraint. |
| Sanitization pipeline | Application | Automatically applies `ISanitizer` before handler execution by reflecting on the instance type for `[Sanitize]` annotations. |
| Sanitizer implementations | Infrastructure | Concrete sanitizers (e.g., HtmlSanitizer-based) |

**Pipeline position:** Sanitization runs **before** validation, before handler execution. Inputs are cleaned first (strip dangerous content), then validated against the sanitized values (reject invalid input), then processed. This ensures validation rules operate on safe, clean data.

```
Request → Sanitization → Validation → Handler
```

**Per-property usage:**

```
public sealed record RegisterUserCommand(
    [property: Sanitize] string Email,
    string DisplayName) : ICommand<ActorId>;
```

**Per-type usage** (auto-applies to all string-shaped properties):

```
[Sanitize]
public sealed record CreatePostCommand(
    string Title,
    string Body,
    IReadOnlyList<string> Tags,
    int Priority) : ICommand<PostId>;
// Title, Body, and Tags are sanitized automatically; Priority is left alone.
```

### Business Event Log

| Concept | Layer | Description |
|---------|-------|-------------|
| `IBusinessEventLog` | Application | Interface for appending log entries |
| `BusinessLogEntry` | Application | Entry type with structured fields |
| Storage implementation | Infrastructure | Persistence — append-only table, queryable by aggregate, tenant, time range |

**Entry structure:**

| Field | Type | Source |
|-------|------|--------|
| `What` | string | Description of what happened (e.g., "Order placed") |
| `AggregateType` | string | Type name of the aggregate involved |
| `AggregateId` | Guid | Underlying value of the aggregate's typed ID. YAF typed IDs are `Guid`-backed (see `TypedId`), so the entry stores the value directly without stringification. |
| `TenantId` | Guid? | From `ITenantContextProvider` |
| `ActorId` | Guid | From `IIdentityContextProvider`. (Renamed from `IdentityId` to align with the `ActorId` typed ID and `IActorScoped` vocabulary used elsewhere in YAF.) |
| `CorrelationId` | Guid | From `ICorrelationIdProvider` |
| `ActivityId` | string? | From `IActivityIdProvider`. W3C trace-context format (e.g., `"00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"`), nullable when no `System.Diagnostics.Activity` is active. |
| `OccurredAtUtc` | DateTimeOffset | System clock |
| `Metadata` | `IReadOnlyDictionary<string, object>?` | Optional additional context. Values should be JSON-serializable primitives or simple structures. |

Context fields are stored as primitive `Guid` / `string` types rather than domain typed-IDs (`TenantId`, `ActorId`) because `BusinessLogEntry` is a DTO that crosses the persistence boundary; readers (queries, projections) shouldn't have to reconstruct domain types just to read a stored value.

**Characteristics:**
- **Append-only** — entries are never modified or deleted
- **Not reactive** — appending a log entry does not trigger handlers or events
- **Human-readable** — descriptions are meaningful to non-technical users ("Order #1234 placed by John Doe"), not technical ("INSERT into Orders")
- **Queryable** — by aggregate, tenant, identity, time range, correlation

**Usage:** Command handlers (or domain event handlers) append entries as part of the operation. The interface uses parameter-based call shape rather than passing a fully constructed `BusinessLogEntry`, so callers don't need to invent placeholder values for context fields the implementation will overwrite:

```
public interface IBusinessEventLog
{
    Task AppendAsync(
        string what,
        string aggregateType,
        Guid aggregateId,
        IReadOnlyDictionary<string, object>? metadata,
        CancellationToken cancellationToken);
}
```

Implementations construct the `BusinessLogEntry` from the supplied parameters plus context fields (`TenantId`, `ActorId`, `CorrelationId`, `ActivityId`, `OccurredAtUtc`) sourced from context providers and a system clock. `AppendAsync` returns plain `Task` (not `Task<Result>`) because audit failures are infrastructure faults — they surface as thrown exceptions, not recoverable business outcomes.

```
await businessEventLog.AppendAsync(
    what: $"Order {order.Id} placed with {order.Items.Count} items",
    aggregateType: nameof(Order),
    aggregateId: order.Id.Value,
    metadata: null,
    cancellationToken: ct);
```

### Application Services

Beyond handlers, the Application layer may contain `IApplicationService` implementations for orchestration that doesn't fit neatly into a single command/query handler (e.g., sagas, multi-step workflows). These are stateless services injected via DI, following the same patterns as handlers for context access and error reporting.

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — context providers (section 21), sanitization (section 18), business event log (section 6)
- [ADR: CQRS and Mediator Abstraction](20260324-1144-cqrs-and-mediator-abstraction.md) — pipeline behaviors
- [ADR: Domain Events](../domain/20260324-1113-domain-events-and-integration-events.md) — context envelope on events
- [ADR: Validation Strategy](../domain/20260324-1141-validation-strategy.md) — validation runs before sanitization in pipeline
