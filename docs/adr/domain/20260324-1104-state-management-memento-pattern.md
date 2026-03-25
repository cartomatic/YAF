# State Management — Memento Pattern

- **Timestamp:** 2026-03-24 11:04
- **Status:** under review
- **Scope:** domain
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF uses the Memento pattern as the persistence boundary between Domain and Infrastructure. Domain objects define an `IMemento<TMemento>` contract declaring how they snapshot and restore their state. Infrastructure owns the concrete memento types (simple DTOs with primitive properties) and passes them to domain objects for population. The domain never exposes its internal state directly — it projects into a provided memento at its own discretion.

## Drivers

1. **Domain encapsulation** — Domain objects expose state via public getters (with private setters) for read access by the application layer, but mutation is controlled through explicit domain methods. The ORM should not drive how domain state is structured or exposed.
2. **Persistence ignorance** — Domain types use rich types (typed IDs, value objects, enumerations). Persistence requires flat primitives (Guid, string, int). The mapping must happen at a well-defined boundary.
3. **ORM independence** — Swapping EF Core for Dapper or a document database should not require changes to domain objects. Only the memento types and their mappings change.
4. **Cross-cutting concern support** — The memento layer is where encryption, versioning snapshots, and graveyard serialization naturally hook in — they operate on the flat DTO, not the rich domain object.
5. **Testability** — Domain logic should be testable without any persistence concern. Memento round-trips (snapshot → restore) should be independently testable.

## Options

### Option A: Memento Pattern (infrastructure passes memento to domain)

Domain objects implement `IMemento<TMemento>` with two methods:
- `Snapshot(TMemento memento)` — domain populates the provided memento
- `Hydrate(TMemento memento)` — domain hydrates itself from the memento

Domain objects have public getters (private setters) for read access — the application layer uses these to project into DTOs. The memento pattern handles the *persistence* boundary specifically: flattening rich types to primitives, hooking encryption/versioning, and keeping ORM mapping out of the domain.

Infrastructure owns the concrete `TMemento` type (a simple DTO), creates instances, and passes them to domain objects.

**Pros:**
- Persistence mapping is isolated from domain structure — ORM cannot drive domain design
- Clean separation: domain knows the shape contract (`TMemento`), infrastructure knows the ORM mapping
- Memento is the natural hook point for encryption, versioning, and graveyard
- ORM-agnostic — memento types can be mapped to EF Core, Dapper, or any storage
- Round-trip testable without a database

**Cons:**
- Boilerplate — every aggregate needs a memento type and snapshot/restore methods
- Two representations of the same data (domain object + memento DTO)
- Developers must keep snapshot/restore in sync with domain state changes

### Option B: EF Core mapping directly to domain objects

Use EF Core's `HasField()`, backing fields, and shadow properties to map directly to domain object state without public getters.

**Pros:**
- Less code — no separate memento types
- EF Core handles the mapping implicitly

**Cons:**
- Domain objects become coupled to EF Core mapping conventions (backing field naming, navigation property patterns)
- Swapping ORMs requires reworking domain object internals
- Cross-cutting concerns (encryption, versioning) must hook into EF Core's change tracker rather than a clean DTO boundary
- "Persistence ignorance" is partial — domain objects must be structured in EF-friendly ways

### Option C: AutoMapper / projection-based mapping

Domain objects expose read-only properties. An external mapper (AutoMapper or manual projection) converts to/from persistence DTOs.

**Pros:**
- Domain objects can have read-only public properties (useful for application layer access too)
- Standard pattern, many developers familiar with it

**Cons:**
- Public read-only properties still expose internal state — just not for writing
- Reflection-based mappers are fragile and hard to debug
- Manual projections are essentially the memento pattern without the formal contract
- No compile-time guarantee that all state is mapped

## Recommendation

**Option A: Memento Pattern.** The boilerplate cost is real but acceptable — it buys genuine encapsulation, ORM independence, and a clean hook point for cross-cutting concerns. The formal `IMemento<TMemento>` contract makes the boundary explicit and testable.

Option B ties the domain to EF Core internals. Option C is the memento pattern without the discipline of a contract.

## Consequences

**Positive:**
- Domain objects have public read-only access (getters with private setters) for application layer DTO projection, while mutation stays controlled through domain methods
- Persistence mapping is decoupled from domain structure — memento types are simple DTOs, easy to map with any ORM, easy to serialize for graveyard/versioning
- Encryption operates on memento properties (`[Encryptable]`), not domain state
- Swapping persistence technology only affects memento types and their EF configuration
- Round-trip tests (`Snapshot` → `Hydrate`) can verify state preservation without a database

**Negative:**
- Every persisted aggregate requires a corresponding memento type — additional code to maintain
- Snapshot/restore methods must be kept in sync with domain state — a forgotten property means data loss
- Two representations of the same data can drift if not tested (mitigated by round-trip tests)

## Conclusion

### Contract

Two interfaces — split to accommodate both immutable value objects and mutable entities:

```
// Yaf.Domain.Interfaces — core memento contract (all persistable domain objects)
public interface IMemento<TSelf, TMemento>
    where TSelf : IMemento<TSelf, TMemento>
    where TMemento : class
{
    void Snapshot(TMemento memento);
    static abstract TSelf Restore(TMemento memento);
}

// Yaf.Domain.Interfaces — optional hydration (mutable entities only)
public interface IHydrateable<TMemento>
    where TMemento : class
{
    void Hydrate(TMemento memento);
}
```

**Why two interfaces?**
- Value objects are immutable records — `Hydrate` (mutate self) is nonsensical. They implement only `IMemento<TSelf, TMemento>`.
- Entities are mutable — they implement both `IMemento` and `IHydrateable`. `Hydrate` reloads state into an existing tracked instance (e.g., EF Core change tracker). `Restore` creates a new instance for initial materialization.
- Infrastructure can check `is IHydrateable<TMemento>` to decide between hydrate-in-place vs restore-as-new.

**C# limitation:** Abstract base types (e.g., `ValueObject<TSelf, TMemento>`) cannot declare `: IMemento<TSelf, TMemento>` because C# does not allow abstract classes to defer `static abstract` interface members to derived types. Concrete types must explicitly implement the interface.

### Flow

**Save:**
```
Repository creates/obtains memento instance
  → passes to domain object via Snapshot(memento)
    → domain populates memento with flattened primitives
      → infrastructure encrypts [Encryptable] properties
        → EF Core persists the memento
```

**Load (new instance — value objects and entities):**
```
EF Core loads memento from database
  → infrastructure decrypts [Encryptable] properties
    → new domain object created via TSelf.Restore(memento)
      → domain reconstructs rich types (typed IDs, value objects, enumerations)
```

**Load (existing instance — entities with IHydrateable only):**
```
EF Core loads memento from database
  → infrastructure decrypts [Encryptable] properties
    → existing domain object reloaded via Hydrate(memento)
      → domain updates internal state from memento
```

### Ownership

| Concern | Owner |
|---------|-------|
| `IMemento<TSelf, TMemento>` interface | Yaf.Domain.Interfaces |
| `IHydrateable<TMemento>` interface | Yaf.Domain.Interfaces |
| `Snapshot` / `Restore` / `Hydrate` methods | Domain objects (consumer-defined) |
| Concrete memento types (DTOs) | Yaf.Infrastructure (consumer-defined) |
| EF Core entity configuration for mementos | Yaf.Infrastructure (consumer-defined) |
| Encryption of `[Encryptable]` properties | Yaf.Infrastructure |
| Versioning snapshots of mementos | Yaf.Infrastructure |
| Graveyard serialization of mementos | Yaf.Infrastructure |

### Type Mapping

| Domain Type | Memento Type |
|-------------|-------------|
| `TypedId<Guid>` (e.g., `OrderId`) | `Guid` |
| `ValueObject` record (e.g., `Address`) | Flattened properties (`string Street`, `string City`, ...) or serialized JSON |
| `Enumeration<TEnum>` | `int` (Id) |
| `DateTime` / `DateTimeOffset` | `DateTimeOffset` |
| Collections of value objects | Serialized JSON or separate memento table |

### Backward Compatibility and Schema Evolution

Domain models evolve — properties are added, removed, or renamed. Since mementos are the persistence boundary, changes to memento types affect stored data.

**Current data (live tables):** `IMemento<TMemento>` is strongly typed. `Hydrate(TMemento)` always receives the current memento shape — EF Core maps the current schema to the current memento type. Schema changes are handled by **EF Core migrations** (adding/removing columns). The `Hydrate` method can assume it always receives a correctly shaped, current memento instance. There is no need for tolerant reading at the domain level.

**Historical data (versioning snapshots and graveyard):** These store mementos as **serialized JSON** at the point in time they were saved. When the memento type evolves, old JSON may no longer match the current `TMemento` shape. The schema evolution concern belongs to the **deserialization layer in Infrastructure**, not to the domain's `Hydrate` method:

- **Deserialization must be a tolerant reader** — when deserializing historical JSON into the current `TMemento` type, the JSON deserializer should handle missing properties (default values), unknown properties (ignore), and type changes gracefully. This is a serializer configuration concern (e.g., `System.Text.Json` with appropriate options).
- **Raw JSON is always retrievable** — regardless of whether deserialization to the current `TMemento` succeeds, the original serialized JSON can be retrieved for manual inspection and visualization. This is the authoritative record of what the state was at that point in time.
- **Graveyard records** follow the same pattern — deserialization handles schema drift, raw JSON is available as fallback.

**Summary:** `Hydrate` operates on strongly typed, current-shape mementos. Schema evolution for historical data is handled by Infrastructure's JSON deserialization, not by the domain.

### Testing

- **Round-trip tests** — create a domain object, snapshot to memento, restore from memento, verify state equality. These tests are unit tests (no database needed) and should exist for every persisted aggregate and value object.
- **Integration tests** — persist via repository (memento → EF Core → database → EF Core → memento → domain), verify full round-trip including any EF Core conventions, query filters, and encryption.
- **Deserialization evolution tests** — when memento shape changes, test that the Infrastructure JSON deserialization handles old memento JSON gracefully (missing properties get defaults, removed properties are ignored). These are infrastructure tests, not domain tests.

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — memento pattern design (section 2)
- [ADR: Domain Building Blocks](20260324-1032-domain-building-blocks.md) — types that implement IMemento
- [ADR: Core Architecture Style](../architecture/20260324-0921-core-architecture-style.md) — dependency rule enforcement
