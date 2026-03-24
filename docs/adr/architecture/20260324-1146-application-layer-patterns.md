# Application Layer Patterns

- **Timestamp:** 2026-03-24 11:46
- **Status:** under review
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
| **`ISanitizable` marker + pipeline sanitization** | **Selected.** Domain marks properties/types that need sanitization. Application pipeline applies it automatically before handler execution. |
| Manual sanitization in handlers | Repetitive, easy to forget. One missed handler is a vulnerability. |
| Sanitization in the API layer only | API can catch HTML in request DTOs, but if commands are constructed from other sources (events, background jobs), those inputs are unsanitized. Application boundary is safer. |

### Business Event Log

| Option | Assessment |
|--------|------------|
| **`IBusinessEventLog` in Application, append-only** | **Selected.** Human-readable log entries for audit and compliance. Not reactive — no handlers triggered. Separate from domain events. |
| Reuse domain events for audit | Domain events are technical signals, not human-readable audit entries. Mixing purposes makes both worse. |
| Infrastructure-level audit log (EF Core interceptor) | Captures data changes, not business intent. "Column X changed from A to B" is not "Customer upgraded to VIP." |

## Recommendation

Dedicated context provider interfaces, pipeline sanitization with `ISanitizable`, and an append-only business event log. All defined in Application, implemented in Infrastructure.

## Consequences

**Positive:**
- Context is available everywhere it's needed (events, audit, accountability) through a clean injection model
- Sanitization is automatic and comprehensive — applied at the pipeline level, not per-handler
- Business event log captures intent, not just data changes — valuable for compliance and customer support
- All three concerns follow the dependency rule — Application defines, Infrastructure implements

**Negative:**
- Four separate context providers means four DI registrations and four constructor parameters where all are needed (mitigated: most code only needs one or two)
- Pipeline sanitization adds a processing step to every command (mitigated: only processes types marked `ISanitizable`)
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
| `ISanitizable` | Domain | Marker interface on types/properties requiring sanitization |
| `ISanitizer` | Application | Interface for sanitization logic (HTML stripping, XSS prevention) |
| Sanitization pipeline | Application | Automatically applies `ISanitizer` to `ISanitizable` inputs before handler execution |
| Sanitizer implementations | Infrastructure | Concrete sanitizers (e.g., HtmlSanitizer-based) |

**Pipeline position:** Sanitization runs **before** validation, before handler execution. Inputs are cleaned first (strip dangerous content), then validated against the sanitized values (reject invalid input), then processed. This ensures validation rules operate on safe, clean data.

```
Request → Sanitization → Validation → Handler
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
| `AggregateType` | string | Type of the aggregate involved |
| `AggregateId` | string | ID of the aggregate |
| `TenantId` | Guid? | From `ITenantContextProvider` |
| `IdentityId` | Guid | From `IIdentityContextProvider` |
| `CorrelationId` | Guid | From `ICorrelationIdProvider` |
| `ActivityId` | Guid | From `IActivityIdProvider` |
| `OccurredAtUtc` | DateTimeOffset | System clock |
| `Metadata` | Dictionary? | Optional additional context |

**Characteristics:**
- **Append-only** — entries are never modified or deleted
- **Not reactive** — appending a log entry does not trigger handlers or events
- **Human-readable** — descriptions are meaningful to non-technical users ("Order #1234 placed by John Doe"), not technical ("INSERT into Orders")
- **Queryable** — by aggregate, tenant, identity, time range, correlation

**Usage:** Command handlers (or domain event handlers) append entries as part of the operation:

```
await businessEventLog.AppendAsync(new BusinessLogEntry
{
    What = $"Order {order.Id} placed with {order.Items.Count} items",
    AggregateType = nameof(Order),
    AggregateId = order.Id.ToString()
});
```

Context fields (tenant, identity, correlation, activity) are auto-populated from context providers — the caller doesn't supply them.

### Application Services

Beyond handlers, the Application layer may contain `IApplicationService` implementations for orchestration that doesn't fit neatly into a single command/query handler (e.g., sagas, multi-step workflows). These are stateless services injected via DI, following the same patterns as handlers for context access and error reporting.

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — context providers (section 21), sanitization (section 18), business event log (section 6)
- [ADR: CQRS and Mediator Abstraction](20260324-1144-cqrs-and-mediator-abstraction.md) — pipeline behaviors
- [ADR: Domain Events](../domain/20260324-1113-domain-events-and-integration-events.md) — context envelope on events
- [ADR: Validation Strategy](../domain/20260324-1141-validation-strategy.md) — validation runs before sanitization in pipeline
