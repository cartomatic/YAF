# Cross-Cutting Infrastructure Concerns

- **Timestamp:** 2026-03-24 12:49
- **Status:** under review
- **Scope:** infrastructure
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

Yaf.Infrastructure provides implementations for several cross-cutting concerns that are defined as contracts in the Domain and Application layers: accountability (who created/modified), timestamping (when), soft-deletion, encryption at rest, versioning with time travel, and a deleted object graveyard. All are opt-in via interfaces/markers on domain objects or mementos, and all operate on memento DTOs — never on domain objects directly.

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
| **`IHasVersionInfo` interface on mementos + automatic EF Core concurrency token** | **Selected.** Memento-only interface enforcing a `Version` property (Guid). Infrastructure auto-configures it as a concurrency token. |
| Concurrency token baked into `AggregateRoot<TId>` | Forces concurrency on all aggregates. Some aggregates (read-heavy, low contention) don't need it. |
| SQL Server `rowversion` / PostgreSQL `xmin` | Database-specific. Not portable. Opaque binary values are harder to reason about than application-managed Guid tokens. |

### Versioning (Time Travel) & Graveyard

| Option | Assessment |
|--------|------------|
| **`IHasVersionHistory` on mementos + automatic snapshots + graveyard** | **Selected.** `IHasVersionHistory` is an independent marker (does not extend `IHasVersionInfo`). Infrastructure stores a memento snapshot (plus version number, who, when) on each UoW commit. On delete, the object is moved to a central graveyard table. Previous states are queryable by version or timestamp. Natural fit with the memento pattern. |
| Event sourcing | Full event replay capability but fundamentally different architecture. Overkill for snapshot-based time travel. Can be added as a separate pattern later. |
| Manual versioning | Consumers implement their own snapshot storage. Repetitive and inconsistent. |
| Separate opt-in for graveyard vs versioning | Additional interface complexity for little benefit — if you care about version history, you care about deletion history too. |

### Soft-Deletion

| Option | Assessment |
|--------|------------|
| **`ISoftDeletable<TActorId>` on domain object + `IHasSoftDelete<T>` on memento + EF Core global query filter** | **Selected.** Opt-in soft-delete via marker interface. `DeletedAtUtc` set on main table, global filter `WHERE DeletedAtUtc IS NULL` applied. Standalone — does not inherit ITimestamped or IAccountable. |
| Per-table `IsDeleted` flag (forced on all entities) | Pollutes every query. Not opt-in. |
| Soft-delete baked into `Entity<TId>` | Forces soft-delete on all entities. Most entities don't need it. |

**Note:** This differs from the originally rejected "per-table soft delete" option because `ISoftDeletable` is **opt-in via a marker interface** with an automatically-applied global query filter. Only entities that explicitly implement `ISoftDeletable` get soft-delete behavior — there is no pollution of entities that don't need it.

### Deleted Object Handling

| Option | Assessment |
|--------|------------|
| **Exclusive-paths model: `ISoftDeletable` for soft-delete, `IHasVersionHistory` for graveyard** | **Selected.** Three-phase lifecycle: active → soft-deleted (if `ISoftDeletable`) → graveyarded (if `IHasVersionHistory`). Each is independent and opt-in. |
| Central graveyard only (no soft-delete) | No way to "undo" a delete without recovery from graveyard. |
| Hard delete for all | Data is gone. No recovery, no audit trail for deletions. |
| Per-table archive table | One archive table per entity table. Schema duplication, migration overhead. |

**Deletion lifecycle:**

| Interfaces | Delete behavior |
|------------|----------------|
| Neither | Hard delete (row gone) |
| `ISoftDeletable` | Soft delete (`DeletedAtUtc` set, row stays in main table) |
| `IHasVersionHistory` | Graveyard (row archived to `DeletedObjects`) |
| Both | Soft delete first; optional permanent delete moves to graveyard later |

## Recommendation

All six concerns as opt-in, interface-based infrastructure features: accountability + timestamping auto-populated on save, soft-deletion via `ISoftDeletable` with global query filter, optimistic concurrency via `IHasVersionInfo`, versioning + graveyard via `IHasVersionHistory` (independent from `IHasVersionInfo`), encryption transparent on memento properties.

## Consequences

**Positive:**
- All concerns are opt-in — consumers activate only what they need via interfaces/markers
- Independent versioning: `IHasVersionInfo` for concurrency, `IHasVersionHistory` for snapshots + graveyard (can be used independently or together)
- Soft-deletion via `ISoftDeletable` with automatic global query filter — opt-in, not forced
- Memento as the hook point means domain objects are completely unaware of these concerns
- Auto-population eliminates manual audit field maintenance
- Graveyard keeps main tables clean while preserving deleted data — only for `IHasVersionHistory` aggregates
- Versioning gets time travel "for free" from the memento pattern — each save already produces a snapshot

**Negative:**
- Graveyard table can grow large in high-churn systems (mitigated: archival/cleanup policies are a consumer concern)
- Versioning snapshots increase storage per save (mitigated: opt-in per aggregate, only for entities that need it)
- Non-versionable, non-soft-deletable aggregates are hard-deleted — no recoverability (by design: opt into `ISoftDeletable` or `IHasVersionHistory`)
- Encryption adds latency to save/load (mitigated: only on marked properties, typically a small subset)
- Consumers must implement `IEncryptionProvider` — YAF provides the hook, not the key management

## Conclusion

### Accountability

| Concept | Layer | Description |
|---------|-------|-------------|
| `IAccountable<TActorId>` | Domain | Interface declaring `CreatedBy`, `ModifiedBy` with generic actor ID type. Non-generic `IAccountable` marker for runtime discovery. |
| `IHasAccountability<T>` | Domain (memento-side) | Memento interface with `T CreatedBy`, `T ModifiedBy` + DIM for boxed access. |
| Auto-population | Infrastructure | `YafDbContext.SaveChanges` reads from `IIdentityContextProvider` and sets fields on mementos |

- `TActorId` is consumer-defined — `UserId`, `EmployeeId`, `ServiceAccountId`, etc.
- Mementos store actor IDs as `Guid` (flattened from typed ID) via `IHasAccountability<Guid>`
- Both `CreatedBy` and `ModifiedBy` are nullable — `null` means the entity has not yet completed a persistence round-trip
- Infrastructure must throw if user context is not provided when saving an accountable entity
- Deletion tracking is a separate concern — see `ISoftDeletable` below

### Timestamping

| Concept | Layer | Description |
|---------|-------|-------------|
| `ITimestamped` | Domain | Interface declaring `CreatedAtUtc?`, `ModifiedAtUtc?` (both nullable `DateTimeOffset?`) |
| `IHasTimestamps` | Domain (memento-side) | Memento interface with `DateTimeOffset? CreatedAtUtc`, `ModifiedAtUtc?` (get/set) |
| Auto-population | Infrastructure | `YafDbContext.SaveChanges` sets timestamps from system clock (UTC) |

- All times are `DateTimeOffset` in UTC
- Both are nullable — `null` means the entity has not yet completed a persistence round-trip
- `CreatedAtUtc` is set on first save, `ModifiedAtUtc` on every subsequent save
- Deletion timestamps are a separate concern — see `ISoftDeletable` below

### Soft-Deletion

| Concept | Layer | Description |
|---------|-------|-------------|
| `ISoftDeletable` | Domain | Non-generic marker. Triggers soft-delete infrastructure behavior. |
| `ISoftDeletable<TActorId>` | Domain | Generic variant with `DeletedAtUtc?` and `TActorId? DeletedBy`. |
| `IHasSoftDelete` / `IHasSoftDelete<T>` | Domain (memento-side) | Memento interface with `DeletedAtUtc?`, `T DeletedBy` + DIM for boxed access. |
| Global query filter | Infrastructure | `WHERE DeletedAtUtc IS NULL` auto-applied to all mementos implementing `IHasSoftDelete`. |
| Auto-population | Infrastructure | `YafDbContext.SaveChanges` sets `DeletedAtUtc` and `DeletedBy` on soft-delete. |

- Standalone interface — does not inherit from `ITimestamped` or `IAccountable` (composition over inheritance)
- `DeletedAtUtc != null` means soft-deleted
- Soft-deleted entities can be un-deleted by clearing `DeletedAtUtc` and `DeletedBy`
- Bypass mechanism for admin queries (similar to tenant filter bypass)
- Coexists with graveyard (`IHasVersionHistory`): soft-delete first, then optional permanent delete to graveyard

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
| `IHasVersionInfo` | Domain (memento-only) | Interface enforcing a `Version` property (Guid) on mementos. |
| Auto-configuration | Infrastructure | When a memento implements `IHasVersionInfo`, `YafDbContext` auto-configures the `Version` property as an EF Core concurrency token. |

- Memento-only — domain objects do not access the version property
- `Version` is a `Guid` — application-managed, portable across databases
- On save: infrastructure generates a new `Guid` for `Version`
- EF Core includes `Version` in the `WHERE` clause of `UPDATE` statements
- On conflict: `DbUpdateConcurrencyException` is thrown

### Versioning (Time Travel) & Graveyard (`IHasVersionHistory`)

| Concept | Layer | Description |
|---------|-------|-------------|
| `IHasVersionHistory` | Domain (memento-only) | Independent marker. Applied to mementos. Opt-in per aggregate. Does not extend `IHasVersionInfo` — concurrency and version history are separate concerns. |
| Version snapshot storage | Infrastructure | On Unit of Work commit: store final memento state + version number + who + when |
| Time-travel queries | Infrastructure | Retrieve any previous state by version number or timestamp |
| Graveyard on delete | Infrastructure | When an `IHasVersionHistory` aggregate is deleted, its memento is serialized to the graveyard table |

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

- **Only `IHasVersionHistory` aggregates** get graveyard treatment
- Without `ISoftDeletable`: entity is hard-deleted from main table and archived to graveyard
- With `ISoftDeletable`: entity can be soft-deleted first, then permanently deleted to graveyard later
- Without either: entity is hard-deleted with no recoverability
- Graveyard has its own independent data model — does not reuse `ISoftDeletable` fields
- Graveyard is append-only from the application's perspective
- Recovery is an explicit operation (restore from graveyard → re-persist)
- Graveyard records are **tenant-scoped** if the original entity was tenant-scoped. Tenant query filters apply to graveyard queries — a tenant can only access their own deleted objects.

### Activation Summary

| Concern | Activated By | Applies To |
|---------|-------------|------------|
| Accountability | `IAccountable<TActorId>` on domain object + `IHasAccountability<T>` on memento | `CreatedBy`, `ModifiedBy` auto-populated from `IIdentityContextProvider` |
| Timestamping | `ITimestamped` on domain object + `IHasTimestamps` on memento | `CreatedAtUtc`, `ModifiedAtUtc` auto-populated from system clock |
| Soft-deletion | `ISoftDeletable<TActorId>` on domain object + `IHasSoftDelete<T>` on memento | `DeletedAtUtc`, `DeletedBy` set; global query filter applied |
| Optimistic concurrency | `IHasVersionInfo` on memento | `Version` (Guid) auto-configured as EF Core concurrency token |
| Versioning + Graveyard | `IHasVersionHistory` on memento | Memento snapshot stored on each save; deleted objects moved to graveyard |
| Encryption | `[Encryptable]` on memento properties | Marked properties encrypted/decrypted during save/load |

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — accountability (14), timestamping (15), encryption (17), versioning (19), graveyard (20)
- [ADR: State Management — Memento Pattern](../domain/20260324-1104-state-management-memento-pattern.md) — memento as the infrastructure hook point
- [ADR: Persistence Strategy](20260324-1229-persistence-strategy.md) — YafDbContext conventions, SaveChanges flow
- [ADR: Application Layer Patterns](../architecture/20260324-1146-application-layer-patterns.md) — context providers for auto-population
