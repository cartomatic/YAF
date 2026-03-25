# Cross-Cutting Infrastructure Concerns

- **Timestamp:** 2026-03-24 12:49
- **Status:** under review
- **Scope:** infrastructure
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

Yaf.Infrastructure provides implementations for several cross-cutting concerns that are defined as contracts in the Domain and Application layers: accountability (who created/modified), timestamping (when), encryption at rest, versioning with time travel, and a deleted object graveyard. All are opt-in via interfaces/markers on domain objects, and all operate on memento DTOs — never on domain objects directly.

## Drivers

1. **Opt-in, not mandatory** — Not every aggregate needs versioning or encryption. These concerns are activated by implementing a marker interface, not by inheriting from a heavy base class.
2. **Memento as the hook point** — Cross-cutting concerns operate on memento DTOs during the persistence flow. This keeps them infrastructure-only — domain objects are unaware of how these concerns are implemented.
3. **Automatic population** — Audit fields (who, when) should be populated automatically from context providers. Developers should not have to remember to set `CreatedBy` or `ModifiedAtUtc` manually.
4. **Recoverability** — Deleted data should be recoverable. Hard deletes lose information permanently.
5. **Compliance** — Encryption at rest, full audit trails, and time travel support common compliance requirements (GDPR, SOX, HIPAA) without consumers building these from scratch.

## Options

### Accountability & Timestamping

| Option | Assessment |
|--------|------------|
| **`IAccountable<TActorId>` + `ITimestamped` interfaces, auto-populated by `YafDbContext`** | **Selected.** Domain declares the contract with generic actor ID. Infrastructure auto-populates from `IIdentityContextProvider` and system clock on `SaveChanges`. |
| Audit fields baked into `Entity<TId>` base | Forces audit on all entities. Some entities (lookup data, configuration) don't need accountability. |
| Manual population in handlers | Error-prone, repetitive, easy to forget. |

### Encryption

| Option | Assessment |
|--------|------------|
| **`[Encryptable]` attribute on memento properties + `IEncryptionProvider`** | **Selected.** Infrastructure encrypts/decrypts marked memento properties transparently during save/load. Consumer provides key management via `IEncryptionProvider`. |
| Transparent Data Encryption (TDE) at database level | Encrypts entire database. No column-level granularity. Doesn't protect against application-level data access. |
| Manual encryption in domain | Domain should not know about encryption. Pollutes business logic with infrastructure concerns. |

### Optimistic Concurrency

| Option | Assessment |
|--------|------------|
| **`IHasVersionInfo` interface on mementos + automatic EF Core concurrency token** | **Selected.** Interface enforces a `Version` property (Guid). When applied to a memento, infrastructure auto-configures it as a concurrency token. Can also be applied to domain objects (e.g., aggregate roots) for domain-level version access, but the EF Core configuration only activates on mementos. |
| Concurrency token baked into `AggregateRoot<TId>` | Forces concurrency on all aggregates. Some aggregates (read-heavy, low contention) don't need it. |
| SQL Server `rowversion` / PostgreSQL `xmin` | Database-specific. Not portable. Opaque binary values are harder to reason about than application-managed Guid tokens. |

### Versioning (Time Travel) & Graveyard

| Option | Assessment |
|--------|------------|
| **`IVersionable : IHasVersionInfo` on mementos + automatic snapshots + graveyard** | **Selected.** `IVersionable` extends `IHasVersionInfo` — any versionable memento also gets optimistic concurrency. Infrastructure stores a memento snapshot (plus version number, who, when) on each UoW commit. On delete, the object is moved to a central graveyard table. Previous states are queryable by version or timestamp. Natural fit with the memento pattern. |
| Event sourcing | Full event replay capability but fundamentally different architecture. Overkill for snapshot-based time travel. Can be added as a separate pattern later. |
| Manual versioning | Consumers implement their own snapshot storage. Repetitive and inconsistent. |
| Separate opt-in for graveyard vs versioning | Additional interface complexity for little benefit — if you care about version history, you care about deletion history too. |

### Deleted Object Handling

| Option | Assessment |
|--------|------------|
| **Central graveyard table, activated by `IVersionable`** | **Selected.** Deleted objects with `IVersionable` mementos are serialized into a `DeletedObjects` table with metadata (type, ID, who, when). Main tables stay clean. Data is recoverable. Non-versionable aggregates are hard-deleted. |
| Per-table soft delete (`IsDeleted` flag) | Pollutes every query with `WHERE IsDeleted = false`. Forgotten filters leak deleted data. Clutters main tables. |
| Hard delete for all | Data is gone. No recovery, no audit trail for deletions. |
| Per-table archive table | One archive table per entity table. Schema duplication, migration overhead. |
| Graveyard for all aggregates | Forces storage overhead on aggregates that don't need recoverability. |

## Recommendation

All five concerns as opt-in, memento-based infrastructure features: optimistic concurrency via `IHasVersionInfo`, accountability + timestamping auto-populated on save, encryption transparent on memento properties, versioning + graveyard via `IVersionable` (which extends `IHasVersionInfo`).

## Consequences

**Positive:**
- All concerns are opt-in — consumers activate only what they need via interfaces/markers
- Two-tier versioning: `IHasVersionInfo` for just concurrency, `IVersionable` for full audit trail (concurrency + snapshots + graveyard)
- Memento as the hook point means domain objects are completely unaware of these concerns
- Auto-population eliminates manual audit field maintenance
- Graveyard keeps main tables clean while preserving deleted data — only for `IVersionable` aggregates
- Versioning gets time travel "for free" from the memento pattern — each save already produces a snapshot

**Negative:**
- Graveyard table can grow large in high-churn systems (mitigated: archival/cleanup policies are a consumer concern)
- Versioning snapshots increase storage per save (mitigated: opt-in per aggregate, only for entities that need it)
- Non-versionable aggregates are hard-deleted — no recoverability (by design: if you need recoverability, opt into `IVersionable`)
- Encryption adds latency to save/load (mitigated: only on marked properties, typically a small subset)
- Consumers must implement `IEncryptionProvider` — YAF provides the hook, not the key management

## Conclusion

### Accountability

| Concept | Layer | Description |
|---------|-------|-------------|
| `IAccountable<TActorId>` | Domain | Interface declaring `CreatedBy`, `ModifiedBy`, `DeletedBy` with generic actor ID type |
| Auto-population | Infrastructure | `YafDbContext.SaveChanges` reads from `IIdentityContextProvider` and sets fields on mementos |

- `TActorId` is consumer-defined — `UserId`, `EmployeeId`, `ServiceAccountId`, etc.
- Mementos store actor IDs as `Guid` (flattened from typed ID)
- `DeletedBy` is populated when an object is moved to the graveyard

### Timestamping

| Concept | Layer | Description |
|---------|-------|-------------|
| `ITimestamped` | Domain | Interface declaring `CreatedAtUtc`, `ModifiedAtUtc`, `DeletedAtUtc` |
| Auto-population | Infrastructure | `YafDbContext.SaveChanges` sets timestamps from system clock (UTC) |

- All times are `DateTimeOffset` in UTC
- `DeletedAtUtc` is set on the graveyard record, not the main entity
- `ModifiedAtUtc` is updated on every save

### Encryption

| Concept | Layer | Description |
|---------|-------|-------------|
| `[Encryptable]` attribute | Domain | Marks memento properties that must be encrypted at rest |
| `IEncryptionProvider` | Infrastructure | Interface for encrypt/decrypt operations. Consumer provides implementation with their key management. |
| Transparent encryption | Infrastructure | During save: encrypt marked properties after `Snapshot`. During load: decrypt marked properties before `Hydrate`. |

**Flow:**
```
Save: Domain → Snapshot(memento) → encrypt [Encryptable] properties → EF Core → database
Load: Database → EF Core → decrypt [Encryptable] properties → Hydrate(memento) → Domain
```

- `string`, `byte[]`, `string[]`, `List<string>`, and `IEnumerable<string>` memento properties can be marked `[Encryptable]` — collections are encrypted element-by-element
- YAF provides the encryption pipeline; consumers provide `IEncryptionProvider` with their key management strategy
- Encryption is transparent to domain objects — they never see encrypted values

**Key rotation:** `IEncryptionProvider` must support key rotation. The provider should be able to decrypt data encrypted with previous keys and encrypt new data with the current key. Consequences of key rotation:
- **Without re-encryption migration:** Old data remains encrypted with old keys. The provider must retain old keys for decryption. Over time, multiple key generations coexist in the database.
- **With re-encryption migration:** A batch process reads, decrypts (old key), re-encrypts (new key), and saves all affected records. Old keys can be retired after migration completes.
- **YAF's role:** YAF provides the encryption/decryption hook in the memento pipeline. Key management, rotation scheduling, and re-encryption migrations are the consumer's responsibility. The `IEncryptionProvider` contract should support a key identifier so encrypted values can be tagged with the key that encrypted them.

### Optimistic Concurrency (`IHasVersionInfo`)

| Concept | Layer | Description |
|---------|-------|-------------|
| `IHasVersionInfo` | Domain | Interface enforcing a `Version` property (Guid). Applicable to domain objects and/or mementos. |
| Auto-configuration | Infrastructure | When a memento implements `IHasVersionInfo`, `YafDbContext` auto-configures the `Version` property as an EF Core concurrency token. |

- `Version` is a `Guid` — application-managed, portable across databases
- On save: infrastructure generates a new `Guid` for `Version`
- EF Core includes `Version` in the `WHERE` clause of `UPDATE` statements
- On conflict: `DbUpdateConcurrencyException` is thrown
- Can be applied to domain objects (e.g., aggregate root) for domain-level version access — but the EF Core concurrency configuration only activates when present on the memento

### Versioning (Time Travel) & Graveyard (`IVersionable`)

| Concept | Layer | Description |
|---------|-------|-------------|
| `IVersionable : IHasVersionInfo` | Domain | Extends `IHasVersionInfo`. Applied to mementos. Opt-in per aggregate. |
| Version snapshot storage | Infrastructure | On Unit of Work commit: store final memento state + version number + who + when |
| Time-travel queries | Infrastructure | Retrieve any previous state by version number or timestamp |
| Graveyard on delete | Infrastructure | When a `IVersionable` aggregate is deleted, its memento is serialized to the graveyard table |

**Snapshot record:**

| Field | Description |
|-------|-------------|
| `AggregateType` | Type discriminator |
| `AggregateId` | Aggregate ID (Guid) |
| `TenantId` | Tenant Guid (if the aggregate is tenant-scoped) |
| `Version` | Incrementing version number |
| `MementoSnapshot` | Serialized memento at this version |
| `ModifiedBy` | Who made this change |
| `ModifiedAtUtc` | When this version was created |

- Version snapshots are **tenant-scoped** if the original aggregate is tenant-scoped. Tenant query filters apply to version snapshot queries — a tenant can only access version history for their own aggregates.
- Version numbers are per-aggregate, monotonically incrementing
- **One snapshot per Unit of Work commit** — intermediate `SaveChangesAsync` calls within a single `ExecuteAsync` do not produce snapshots. Only the final committed state is versioned. This avoids noisy partial snapshots from multi-step operations.
- Snapshots are stored as serialized mementos (JSON) — independent of the current schema
- **Time-travel queries** return the memento as it was serialized at that point in time:
  - `GetAtVersion(id, version)` or `GetAtTime(id, timestamp)` deserializes the historical JSON into the current `TMemento` type and hydrates a domain object for programmatic access. The JSON deserializer handles schema drift tolerantly (missing properties get defaults, unknown properties are ignored).
  - The raw serialized JSON is also retrievable for manual inspection and visualization — useful when the domain model has evolved significantly and tolerant deserialization cannot fully reconstruct the state.
- **Schema evolution:** Snapshots are stored as JSON. The **deserialization layer** handles schema drift — not the domain's `Hydrate` method, which always operates on a strongly typed, current-shape memento (see State Management ADR). The raw JSON serves as the authoritative record of what the state was at that point in time.

**Graveyard record:**

| Field | Description |
|-------|-------------|
| `ObjectType` | Type discriminator |
| `ObjectId` | Original aggregate ID (Guid) |
| `SerializedState` | Memento snapshot at time of deletion (JSON) |
| `DeletedBy` | Actor Guid (from `IIdentityContextProvider`) |
| `DeletedAtUtc` | When deleted |
| `TenantId` | Tenant Guid (if tenant-scoped) |

- **Only `IVersionable` aggregates** get graveyard treatment — non-versionable aggregates are hard-deleted
- Main entity is removed from its table — no soft-delete flags, no query filter pollution
- Graveyard is append-only from the application's perspective
- Recovery is an explicit operation (restore from graveyard → re-persist)
- Graveyard records are **tenant-scoped** if the original entity was tenant-scoped. Tenant query filters apply to graveyard queries — a tenant can only access their own deleted objects.

### Activation Summary

| Concern | Activated By | Applies To |
|---------|-------------|------------|
| Optimistic concurrency | Implement `IHasVersionInfo` on memento | `Version` (Guid) auto-configured as EF Core concurrency token |
| Accountability | Implement `IAccountable<TActorId>` on domain object | Memento gets `CreatedBy`, `ModifiedBy` auto-populated |
| Timestamping | Implement `ITimestamped` on domain object | Memento gets `CreatedAtUtc`, `ModifiedAtUtc` auto-populated |
| Encryption | Mark memento properties with `[Encryptable]` | Marked properties encrypted/decrypted during save/load |
| Versioning + Graveyard | Implement `IVersionable` on memento | Memento snapshot stored on each save; deleted objects moved to graveyard |

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — accountability (14), timestamping (15), encryption (17), versioning (19), graveyard (20)
- [ADR: State Management — Memento Pattern](../domain/20260324-1104-state-management-memento-pattern.md) — memento as the infrastructure hook point
- [ADR: Persistence Strategy](20260324-1229-persistence-strategy.md) — YafDbContext conventions, SaveChanges flow
- [ADR: Application Layer Patterns](../architecture/20260324-1146-application-layer-patterns.md) — context providers for auto-population
