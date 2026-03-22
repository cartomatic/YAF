# YAF DDD Concepts Brainstorm

**Date:** 2026-03-22
**Status:** Draft
**Participants:** Human (architect/decision-maker), Claude Code (research & facilitation)
**Parent:** [YAF Library Design Brainstorm](20260322-1755-yaf-library-design-brainstorm.md)

## What We're Building

The domain modeling foundation for YAF — the base types, interfaces, and conventions that consumers use to build rich domain models. This covers everything from entities and aggregates through to cross-cutting concerns like accountability, encryption, and versioning.

**Design philosophy:** Domain objects are the center of the universe. They define their own contracts (memento shape, validation rules, error catalog). Infrastructure adapts to them, never the reverse. Construction is controlled (no public constructors), state is encapsulated, and cross-cutting concerns are opt-in via interfaces.

## Layer Responsibilities

### Yaf.Domain — Pure .NET, zero dependencies

Owns: building blocks, identity, state contracts, event contracts, validation, error definitions, domain services, specifications, construction patterns.

**Nothing in this layer knows about persistence, HTTP, or any framework.** It is the innermost ring.

### Yaf.Application — Depends on Yaf.Domain

Owns: CQRS pipeline, context providers (tenant, identity, correlation, activity), sanitization pipeline, business event log, error catalog discovery, application services, DTO contracts.

**Orchestrates domain operations.** Receives commands/queries, resolves context, delegates to domain, returns results.

### Yaf.Infrastructure — Depends on Yaf.Domain + Yaf.Application

Owns: persistence (memento ↔ EF Core mapping), encryption, accountability auto-population, versioning snapshots, deleted object graveyard, context provider implementations, job scheduling, business event log storage.

**Implements all infrastructure contracts** defined by Domain and Application.

---

## Concept Catalog

### 1. Building Blocks (Domain)

| Concept | Description |
|---------|-------------|
| `Entity<TId>` | Base entity with typed identity. Equality by ID. |
| `AggregateRoot<TId>` | Entity that serves as a consistency boundary. Owns domain event collection. Carries optimistic concurrency token. |
| `ValueObject` | Immutable, equality by value. No identity. |
| `Enumeration<TEnum>` | Smart enum base — Id + Name + behavior. Replaces raw C# enums for domain concepts with logic. |
| `TypedId<T>` | Record-based strongly-typed ID. Wraps a value (typically Guid). No raw GUIDs in the domain model. |

### 2. State Management — Memento Pattern (Domain contract, Infrastructure implementation)

| Concept | Layer | Description |
|---------|-------|-------------|
| `IMemento<TMemento>` | Domain | Interface declaring `SnapshotTo(TMemento)` and `RestoreFrom(TMemento)`. Domain objects populate/restore from a memento provided by infrastructure. |
| Concrete memento types | Infrastructure | Simple DTOs with primitive types, properly configured for EF Core or any ORM. |
| Memento encryption | Infrastructure | Properties marked `[Encryptable]` on memento types are transparently encrypted/decrypted during persistence. `IMemento` interface enforces encryptability of marked properties. |

**Flow (save):** Repository creates/provides a memento instance → passes it to domain object → domain object populates it via `SnapshotTo(memento)` → infrastructure encrypts marked properties → EF Core → database.

**Flow (load):** Database → EF Core → memento DTO → infrastructure decrypts → domain object hydrates via `RestoreFrom(memento)`.

The key insight: **infrastructure passes the memento to the domain object**, not the other way around. The domain object snapshots its state into the provided memento at its own discretion. This keeps domain objects fully encapsulated — infrastructure never reaches into domain state directly. The memento type is owned by infrastructure; the domain only knows the generic `TMemento` contract.

Domain objects use rich types (typed IDs, value objects). Mementos flatten to simple types (Guid, string, int). Application layer is memento-agnostic.

### 3. Identity — Typed IDs (Domain)

Record-based typed IDs. Each aggregate/entity defines its own ID type:

```
OrderId : TypedId<Guid>
CustomerId : TypedId<Guid>
```

No raw GUIDs in the domain model. Prevents accidental ID mix-ups at compile time. Mementos convert typed IDs to plain Guids for persistence.

### 4. Construction Patterns (Domain)

**No public constructors.** Domain objects are created through:

| Pattern | When to use |
|---------|-------------|
| Static factory methods | Simple creation: `Order.Create(customerId, items)`. Enforces invariants, raises creation event. |
| `Builder<TEntity, TBuilder>` | Complex creation: fluent `.WithCustomer(id).WithItems(items).Build()`. YAF provides a generic base. Consumer extends per aggregate. |

Both patterns call private constructors internally. Invariants are enforced at creation time.

### 5. Events (Domain)

All events automatically carry a context envelope: TenantId, IdentityId, CorrelationId, ActivityId — populated from context providers. Events are always traceable.

| Event type | Purpose | Scope |
|-----------|---------|-------|
| `IDomainEvent` | Internal signal within a bounded context. Triggers side effects (update read model, enforce policy). | In-process |
| `IIntegrationEvent` | Cross-bounded-context communication. Published to a message broker. | Cross-service |

### 6. Business Event Log (Application)

An append-only stream of human-readable log entries describing what happened in a business process. **Not reactive** — no handlers triggered. Used for audit, compliance, and business process visibility.

| Concept | Layer | Description |
|---------|-------|-------------|
| `IBusinessEventLog` | Application | Interface for appending log entries. |
| `BusinessLogEntry` | Application | Entry with: what happened, when, who (identity), tenant, correlation/activity IDs. |
| Storage | Infrastructure | Persistence for the append-only stream. Queryable by aggregate, tenant, time range. |

Example (parcel lifecycle): Created → PickedUp → AtDistributionCentre → InTransit → OutForDelivery → Delivered.

Carries the same context envelope as events (tenant, identity, correlation, activity).

### 7. Jobs — Lifecycle-Aware Processes (Domain concept, Infrastructure scheduling)

| Concept | Layer | Description |
|---------|-------|-------------|
| `Job` base type | Domain | First-class domain concept with lifecycle states: Scheduled → Running → Completed / Failed / Cancelled. |
| Job scheduling | Infrastructure | Persistence and execution infrastructure. Immediate or deferred. |

Jobs are domain objects — they have identity, state, accountability, and can raise events.

### 8. Specifications (Domain)

`ISpecification<T>` with composable And/Or/Not operations. Encapsulates query criteria for repositories.

| Concept | Layer | Description |
|---------|-------|-------------|
| `ISpecification<T>` | Domain | Interface with composable criteria (filter, include, order, page). |
| Specification evaluator | Infrastructure | Translates specifications to EF Core / ORM queries. |

Keeps `IRepository<T>` generic. Query logic is testable and reusable.

### 9. Services (Domain + Application)

| Concept | Layer | Description |
|---------|-------|-------------|
| `IDomainService` | Domain | Marker interface. Domain logic that spans multiple aggregates. Stateless. Must not depend on infrastructure. |
| `IApplicationService` | Application | Marker interface. Application-level orchestration that doesn't fit into a command/query handler. |

### 10. Policies (Domain)

Policies encapsulate business rules that are **corrective** rather than preventive. While invariants say "reject this if invalid," policies say "when this condition occurs, take this action."

| Concept | Layer | Description |
|---------|-------|-------------|
| `IPolicy<TEvent>` | Domain | Reacts to a domain event and determines what corrective action to take. |
| `IPolicyRule` | Domain | A single evaluable business rule. Returns whether it applies and what action to recommend. |

**Invariants vs Policies:**
- **Invariant** (preventive): "An order cannot exceed $10,000 without manager approval" → reject the command.
- **Policy** (corrective): "When a customer's total spend exceeds $50,000, flag the account for VIP upgrade" → react to the event, trigger a follow-up.

Policies react to domain events and can trigger commands, raise new events, or create jobs. They are first-class domain concepts — testable, composable, and explicit about the business rules they encode.

### 11. Result Pattern (Domain)

YAF ships its own `Result<T>` in Yaf.Domain — lightweight, integrates with the `IError` hierarchy, zero external dependencies.

Domain methods: `Result<Order> Order.AddItem(...)` — success or domain error.
Application handlers: return `Result<TResponse>` — flows to API layer.
API layer: maps `Result<T>` to ProblemDetails responses.

### 12. Error Hierarchy (Domain + Application)

```
IError
├── IDomainError      (domain-level errors: invariant violations, business rule failures)
└── IApplicationError (application-level errors: not found, unauthorized, validation failures)
```

| Concept | Layer | Description |
|---------|-------|-------------|
| `IError` | Domain | Base error interface. |
| `IDomainError` | Domain | Domain-specific errors. Declared on entities/aggregates. |
| `IApplicationError` | Application | Application-level errors. Declared on handlers/services. |
| `IErrorSource` | Domain | Marker interface. Applied to domain objects and application handlers alike. System auto-discovers all declared errors. |
| Error catalog discovery | Application | Scans all `IErrorSource` implementations (domain + application), collects all errors. Exposable via API endpoint. |

### 13. Validation (Domain + Application)

| Concept | Layer | Description |
|---------|-------|-------------|
| `IValidatable` | Domain | Domain objects can self-validate (invariant enforcement). |
| `IValidator<T>` | Domain | Validator interface — usable at domain level for complex validation rules. |
| Validation pipeline | Application | Validates commands/queries before handler execution. |
| FluentValidation integration | API | Wires FluentValidation into the pipeline. |

### 14. Accountability (Domain contract, Infrastructure auto-population)

`IAccountable<TActorId>` — generic typed ID so consumers use their own identity model.

| Field | Type | Description |
|-------|------|-------------|
| CreatedBy | TActorId | Who created the object |
| ModifiedBy | TActorId? | Who last modified it |
| DeletedBy | TActorId? | Who deleted it (graveyard scenario) |

Persisted as Guid via memento. Auto-populated by infrastructure from `IIdentityContextProvider`.

### 15. Timestamping (Domain contract, Infrastructure auto-population)

| Field | Type | Description |
|-------|------|-------------|
| CreatedAtUtc | DateTimeOffset | When created |
| ModifiedAtUtc | DateTimeOffset? | When last modified |
| DeletedAtUtc | DateTimeOffset? | When deleted (stored in graveyard, see section 20) |

All times UTC. Auto-populated by infrastructure. Accountability (section 14) and timestamping work together — both are auto-populated from context providers. Deletion fields appear on the graveyard record, not on the main entity table.

### 16. Optimistic Concurrency (Domain + Infrastructure)

`AggregateRoot<TId>` carries a concurrency token. Infrastructure maps it to EF Core's `[ConcurrencyCheck]` / `RowVersion`. Prevents last-write-wins.

### 17. Encryptability (Domain marker, Infrastructure implementation)

Properties marked `[Encryptable]` on memento types are transparently encrypted/decrypted. `IMemento` enforces encryptability of marked properties.

| Concept | Layer | Description |
|---------|-------|-------------|
| `[Encryptable]` attribute | Domain | Marks properties that must be encrypted at rest. |
| `IEncryptionProvider` | Infrastructure | Injectable encryption/decryption. Consumer provides their key management. |

### 18. Sanitization (Application pipeline)

Input sanitization (XSS, HTML stripping) applied at the adapter → application boundary.

| Concept | Layer | Description |
|---------|-------|-------------|
| `ISanitizable` | Domain | Marker interface for objects/properties requiring sanitization. |
| Sanitization pipeline | Application | Auto-sanitizes marked inputs before they reach domain logic. |

### 19. Versioning — Time Travel (Domain marker, Infrastructure implementation)

When a domain object is marked as `IVersionable`, infrastructure automatically stores memento snapshots on each mutation. Previous states are queryable — full time-travel capability.

| Concept | Layer | Description |
|---------|-------|-------------|
| `IVersionable` | Domain | Marker interface. Opt-in per aggregate/entity. |
| Version snapshots | Infrastructure | Stores memento + version number + who + when on each save. |
| Time-travel queries | Infrastructure | Retrieve any previous state by version or timestamp. |

### 20. Deleted Object Graveyard (Infrastructure)

Instead of per-table soft-delete flags, deleted objects are moved to a central `DeletedObjects` table:

| Field | Description |
|-------|-------------|
| ObjectType | Type discriminator |
| ObjectId | Original ID (Guid) |
| SerializedState | Memento snapshot at time of deletion |
| DeletedBy | Actor Guid |
| DeletedAtUtc | When |

Keeps main tables clean. Deleted data is recoverable.

### 21. Context Providers (Application interfaces, Infrastructure implementations)

| Provider | Purpose | Propagated to |
|----------|---------|---------------|
| `ITenantContextProvider` | Tenant the operation is scoped to | Events, business log, queries |
| `IIdentityContextProvider` | User performing the operation | Events, business log, accountability |
| `ICorrelationIdProvider` | Cross-service operation correlation | Events, business log |
| `IActivityIdProvider` | Within-application activity identity | Events, business log |

All four are automatically injected into event envelopes, business log entries, and accountability fields. Enables full traceability from API request through domain events to integration events across services.

### 22. Multi-Tenancy (Application + Infrastructure)

First-class from day one. Opt-in per aggregate.

| Concept | Layer | Description |
|---------|-------|-------------|
| `ITenantContextProvider` | Application | Resolves current tenant. |
| Tenant query filters | Infrastructure | Automatic `WHERE TenantId = @current` on all tenant-scoped queries. |
| Tenant resolution | Infrastructure (API) | Resolves tenant from request (header, subdomain, JWT claim). |

---

## Concept-to-Layer Matrix

| Concept | Domain | Application | Infrastructure |
|---------|--------|-------------|----------------|
| Entity, AggregateRoot, ValueObject | **Define** | | |
| Enumeration (smart enum) | **Define** | | EF conversion |
| TypedId | **Define** | | Memento → Guid |
| Memento (IMemento) | **Interface** | | **Implement** (concrete types, EF mapping) |
| Domain Events | **Define + raise** | | Dispatch |
| Integration Events | **Define** | Publish | Broker adapter |
| Business Event Log | | **Interface + entry type** | **Storage** |
| Jobs | **Define (lifecycle)** | | **Scheduling + persistence** |
| Specifications | **Define** | | **Evaluator** |
| Domain Services | **Define + implement** | | |
| Application Services | | **Define + implement** | |
| Result pattern | **Define** | **Return from handlers** | |
| Error hierarchy (IError) | **IDomainError** | **IApplicationError** | |
| Error catalog (IErrorSource) | **Declare errors** | **Discovery service** | |
| Validation (IValidatable, IValidator) | **Define** | **Pipeline** | |
| Accountability (IAccountable) | **Interface** | | **Auto-populate** |
| Timestamping | **Interface** | | **Auto-populate** |
| Optimistic concurrency | **Token on AggregateRoot** | | **EF mapping** |
| Encryptability | **Attribute** | | **Encrypt/decrypt** |
| Sanitization | **Marker** | **Pipeline** | |
| Versioning (IVersionable) | **Marker** | | **Snapshot storage + queries** |
| Deleted object graveyard | | | **Graveyard table** |
| Policies (IPolicy, IPolicyRule) | **Define** | Handler wiring | |
| Construction (factory + builder) | **Define** | | |
| Context providers | | **Interfaces** | **Implementations** |
| Multi-tenancy | | **ITenantContextProvider** | **Query filters + resolution** |

---

## Key Decisions

### 1. Memento as the persistence boundary (infrastructure-driven)

Domain objects use rich types (typed IDs, value objects, enumerations). Mementos flatten to simple types for ORM mapping. Infrastructure owns the concrete memento types and passes them to domain objects for population (`SnapshotTo`) or hydration (`RestoreFrom`). Domain never exposes its internal state directly — it snapshots into a provided memento at its own discretion. Application is memento-agnostic.

**Rationale:** Keeps domain fully encapsulated. Infrastructure cannot reach into domain state. ORM configuration is entirely an infrastructure concern. Swapping EF Core for Dapper or another ORM only requires new memento implementations.

### 2. Error hierarchy with auto-discovery

`IError` → `IDomainError` / `IApplicationError`. Errors declared on domain objects and handlers via `IErrorSource`. System discovers all errors automatically, enabling a complete error catalog API.

**Rationale:** Consumers can expose a full error catalog to API clients (for documentation, client-side validation, error handling). Errors are distinguishable by layer but share common behavior.

### 3. Context envelope on all events

Every domain event, integration event, and business log entry automatically carries TenantId, IdentityId, CorrelationId, and ActivityId. Populated from context providers.

**Rationale:** Full traceability from source operation through the entire event chain. No opt-in needed — events are always traceable.

### 4. Graveyard table instead of per-table soft delete

Deleted objects are serialized (via memento) into a central `DeletedObjects` table. Main tables stay clean.

**Rationale:** Soft delete flags pollute every query with `WHERE IsDeleted = false`. A central graveyard keeps table scans efficient and data recoverable.

### 5. Construction: factory methods + generic builder

No public constructors. Static factory methods for simple creation, `Builder<TEntity, TBuilder>` base for complex/fluent construction. Both enforce invariants.

**Rationale:** Prevents invalid object creation. Builder pattern enables readable, composable construction for aggregates with many properties.

### 6. Accountability with generic actor ID

`IAccountable<TActorId>` — consumer provides their own identity typed ID. Persisted as Guid via memento.

**Rationale:** YAF doesn't impose an identity model. Consumer's `UserId`, `EmployeeId`, or `ServiceAccountId` all work.

### 7. Automatic versioning via memento snapshots

`IVersionable` marker triggers automatic memento snapshot storage on each save. Full time-travel: query any previous state by version number or timestamp.

**Rationale:** Developer just marks an entity as versionable. Infrastructure handles the rest. Natural fit with the memento pattern — each save already produces a snapshot.

### 8. Multi-tenancy from day one

`ITenantContextProvider` in Application, automatic query filters in Infrastructure. Opt-in per aggregate.

**Rationale:** Retrofitting multi-tenancy is painful. Designing for it from the start (even if opt-in) prevents architectural debt.

### 9. Validation at both domain and application levels

`IValidator<T>` exists in Domain (for complex domain validation rules) and Application (for command/query validation pipeline). `IValidatable` allows domain objects to self-validate.

**Rationale:** Some validation is inherently domain logic (business rules). Some is application-level (input shape, required fields). Both need first-class support.

## Open Questions

*None — all key questions resolved during brainstorm.*

## Research References

- [DDD tactical patterns research](../research/20260322-0935-dotnet10-web-api-framework-best-practices.md) — .NET 10 patterns
- [Parent brainstorm](20260322-1755-yaf-library-design-brainstorm.md) — package structure, technology choices
- [Project manifesto](../general/what-and-why-or-the-other-way-round.md) — project vision
