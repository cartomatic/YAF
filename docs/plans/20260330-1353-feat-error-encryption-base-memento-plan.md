---
title: "feat: Error source, encryption markers, and base memento classes"
type: feat
status: active
date: 2026-03-30
---

# feat: Error Source, Encryption Markers, and Base Memento Classes

## Overview

Add four groups of domain building blocks to `Yaf.Domain`:

1. **Error infrastructure** — `IErrorSource` marker interface and `Error` record implementing `IError`
2. **Encryption markers** — `IEncryptable` marker interface and `[Encrypt]` attribute for memento properties
3. **Composite memento interfaces** — `IMementoBase` (with and without tenant) combining existing `IHas*` interfaces
4. **Base memento classes** — Abstract classes implementing `IMementoBase` variants, eliminating boilerplate for consumers

All four are pure domain-layer concerns: zero dependencies, no infrastructure knowledge.

## Problem Statement / Motivation

**Error infrastructure:** The `IError` interface exists but there is no concrete implementation and no way to discover which classes define errors. Consumers need a simple `Error` type to declare error constants, and `IErrorSource` enables future automated error catalog extraction (translations, documentation, API error endpoints).

**Encryption markers:** The [brainstorm](../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) defines `[Encryptable]` attributes on memento types for transparent encryption at rest. Neither the marker interface nor the attribute exists yet. Infrastructure needs these markers to identify which memento properties to encrypt/decrypt during persistence.

**Composite memento interfaces + base classes:** Every memento class currently must implement 4–5 individual `IHas*` interfaces and repeat the same property declarations. A composite interface + abstract base class eliminates this boilerplate while preserving opt-in flexibility.

## Proposed Solution

### 1. Error Infrastructure

#### `IErrorSource` — Marker Interface

```csharp
// src/Yaf.Domain/Interfaces/IErrorSource.cs
namespace Yaf.Domain.Interfaces;

/// <summary>
/// Marker interface for types that declare <see cref="IError"/> properties or fields.
/// Infrastructure scans implementations to build an error catalog for
/// documentation, translations, and API error endpoints.
/// </summary>
public interface IErrorSource;
```

**Design notes:**
- Pure marker — no members. Discovery is reflection-based at startup.
- Applied to domain entities, aggregates, value objects, or dedicated error classes.
- Application layer will provide the catalog discovery service (future scope).

#### `Error` — Concrete `IError` Implementation

```csharp
// src/Yaf.Domain/Error.cs
namespace Yaf.Domain;

/// <summary>
/// Immutable error with a machine-readable code and human-readable message.
/// </summary>
/// <param name="Code">Machine-readable error code.</param>
/// <param name="Message">Human-readable error message.</param>
public sealed record Error(string Code, string Message) : IError;
```

**Design notes:**
- Sealed record — immutable, value equality, concise.
- Guard against `null`/empty `Code` and `Message` in the constructor.
- Consumers declare errors as `static readonly` fields:

```csharp
public class Order : AggregateRoot<OrderId, Order, OrderMemento>, IErrorSource
{
    public static readonly IError EmptyCart = new Error("ORDER_EMPTY_CART", "Cannot create an order with an empty cart.");
    public static readonly IError ExceedsLimit = new Error("ORDER_EXCEEDS_LIMIT", "Order total exceeds the allowed limit.");
}
```

### 2. Encryption Markers

#### `IEncryptable` — Marker Interface

```csharp
// src/Yaf.Domain/Interfaces/IEncryptable.cs
namespace Yaf.Domain.Interfaces;

/// <summary>
/// Marker interface for memento types containing properties that require encryption at rest.
/// Infrastructure encrypts/decrypts properties marked with <see cref="EncryptAttribute"/>
/// during persistence operations.
/// </summary>
/// <remarks>
/// <para>
/// Supported property types for encryption:
/// <see cref="string"/>, <see cref="T:string[]"/>,
/// <see cref="List{T}"/> where T is <see cref="string"/>,
/// and <see cref="T:byte[]"/>.
/// </para>
/// <para>
/// Mementos implementing this interface signal to infrastructure that they contain
/// sensitive data requiring encryption. The actual encryption/decryption is performed
/// by an <c>IEncryptionProvider</c> (defined in infrastructure).
/// </para>
/// </remarks>
public interface IEncryptable;
```

#### `EncryptAttribute` — Property-Level Attribute

```csharp
// src/Yaf.Domain/Attributes/EncryptAttribute.cs
namespace Yaf.Domain.Attributes;

/// <summary>
/// Marks a memento property for transparent encryption at rest.
/// Infrastructure encrypts the value before persistence and decrypts after retrieval.
/// </summary>
/// <remarks>
/// <para>Valid on properties of type:</para>
/// <list type="bullet">
///   <item><description><see cref="string"/></description></item>
///   <item><description><see cref="T:string[]"/></description></item>
///   <item><description><see cref="List{T}"/> where T is <see cref="string"/></description></item>
///   <item><description><see cref="T:byte[]"/></description></item>
/// </list>
/// <para>
/// The containing memento class must implement <see cref="IEncryptable"/>.
/// Applying this attribute to unsupported types will cause a runtime exception
/// during infrastructure initialization.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class EncryptAttribute : Attribute;
```

**Design notes:**
- `AttributeTargets.Property` only — encryption applies to individual properties.
- `Inherited = true` — derived memento classes inherit encryption markers.
- The attribute name is `Encrypt` (not `Encryptable`) to read naturally: `[Encrypt] public string SocialSecurityNumber { get; set; }`.
- Runtime validation (infrastructure concern, future scope): verify the property type is in the supported set, verify the memento implements `IEncryptable`.

### 3. Composite Memento Interfaces

#### `IMementoBase` — Without Tenant

```csharp
// src/Yaf.Domain/Interfaces/IMementoBase.cs
namespace Yaf.Domain.Interfaces;

/// <summary>
/// Composite memento interface combining the standard cross-cutting concerns
/// for non-tenant-scoped entities: identity, accountability, timestamps, and version info.
/// </summary>
/// <remarks>
/// Use <see cref="ITenantMementoBase"/> for tenant-scoped entities.
/// Consumers can implement this interface directly or extend <see cref="MementoBase"/>
/// to get default property implementations.
/// </remarks>
public interface IMementoBase : IHasIdentity, IHasAccountability, IHasTimestamps, IHasVersionInfo;
```

#### `ITenantMementoBase` — With Tenant

```csharp
// src/Yaf.Domain/Interfaces/ITenantMementoBase.cs
namespace Yaf.Domain.Interfaces;

/// <summary>
/// Composite memento interface combining the standard cross-cutting concerns
/// for tenant-scoped entities: identity, accountability, timestamps, version info, and tenant.
/// </summary>
/// <remarks>
/// Use <see cref="IMementoBase"/> for non-tenant-scoped entities.
/// Consumers can implement this interface directly or extend <see cref="TenantMementoBase"/>
/// to get default property implementations.
/// </remarks>
public interface ITenantMementoBase : IMementoBase, IHasTenantId;
```

**Design notes:**
- `IMementoBase` does **not** include `IHasSoftDelete` or `IHasVersionHistory` — these are independent, opt-in concerns that consumers add when needed.
- `ITenantMementoBase` extends `IMementoBase` (not a parallel hierarchy) — avoids duplication.
- Naming: `ITenantMementoBase` rather than `IMementoBaseWithTenant` — shorter, follows `ITenantScoped` naming convention.

### 4. Base Memento Classes

#### `MementoBase` — Without Tenant

```csharp
// src/Yaf.Domain/MementoBase.cs
namespace Yaf.Domain;

/// <summary>
/// Abstract base class for non-tenant-scoped mementos, providing default property
/// implementations for <see cref="IMementoBase"/>: identity, accountability,
/// timestamps, and version info.
/// </summary>
/// <remarks>
/// Consumers extend this class and add entity-specific properties.
/// For tenant-scoped mementos, use <see cref="TenantMementoBase"/>.
/// For mementos requiring soft-delete or version history, also implement
/// <see cref="IHasSoftDelete"/> or <see cref="IHasVersionHistory"/> directly.
/// </remarks>
public abstract class MementoBase : IMementoBase
{
    /// <inheritdoc />
    public Guid? Id { get; set; }

    /// <inheritdoc />
    public Guid? CreatedBy { get; set; }

    /// <inheritdoc />
    public Guid? ModifiedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public Guid Version { get; set; }
}
```

#### `TenantMementoBase` — With Tenant

```csharp
// src/Yaf.Domain/TenantMementoBase.cs
namespace Yaf.Domain;

/// <summary>
/// Abstract base class for tenant-scoped mementos, providing default property
/// implementations for <see cref="ITenantMementoBase"/>: identity, accountability,
/// timestamps, version info, and tenant.
/// </summary>
/// <remarks>
/// Consumers extend this class and add entity-specific properties.
/// For non-tenant-scoped mementos, use <see cref="MementoBase"/>.
/// For mementos requiring soft-delete or version history, also implement
/// <see cref="IHasSoftDelete"/> or <see cref="IHasVersionHistory"/> directly.
/// </remarks>
public abstract class TenantMementoBase : MementoBase, ITenantMementoBase
{
    /// <inheritdoc />
    public Guid? TenantId { get; set; }
}
```

**Design notes:**
- `TenantMementoBase` extends `MementoBase` — no property duplication.
- Both are `abstract` — never instantiated directly, always extended by consumer mementos.
- Properties use `{ get; set; }` — required for EF Core mapping and memento population.
- No constructor logic — mementos are plain DTOs.

## Technical Considerations

### Namespace & File Organization

| Type | Namespace | File Path |
|------|-----------|-----------|
| `IErrorSource` | `Yaf.Domain.Interfaces` | `src/Yaf.Domain/Interfaces/IErrorSource.cs` |
| `IEncryptable` | `Yaf.Domain.Interfaces` | `src/Yaf.Domain/Interfaces/IEncryptable.cs` |
| `IMementoBase` | `Yaf.Domain.Interfaces` | `src/Yaf.Domain/Interfaces/IMementoBase.cs` |
| `ITenantMementoBase` | `Yaf.Domain.Interfaces` | `src/Yaf.Domain/Interfaces/ITenantMementoBase.cs` |
| `Error` | `Yaf.Domain` | `src/Yaf.Domain/Error.cs` |
| `EncryptAttribute` | `Yaf.Domain.Attributes` | `src/Yaf.Domain/Attributes/EncryptAttribute.cs` |
| `MementoBase` | `Yaf.Domain` | `src/Yaf.Domain/MementoBase.cs` |
| `TenantMementoBase` | `Yaf.Domain` | `src/Yaf.Domain/TenantMementoBase.cs` |

### Guard Clauses in `Error`

The `Error` record constructor should validate:
- `Code` is not null or whitespace
- `Message` is not null or whitespace

Throw `ArgumentException` on violation. This prevents silent creation of invalid errors.

### Attribute Placement

`EncryptAttribute` lives in `Yaf.Domain.Attributes` — a new namespace/folder. This follows .NET conventions (e.g., `System.ComponentModel.DataAnnotations`) and keeps attributes separate from interfaces.

### Why Not Include `IHasSoftDelete` in `IMementoBase`?

Soft-delete is a distinct concern from the standard CRUD lifecycle:
- Most entities don't need soft-delete (the graveyard pattern via `IHasVersionHistory` is the preferred approach per brainstorm)
- Including it in the base would force unused `DeletedAtUtc`/`DeletedBy` columns on every table
- Consumers who need both soft-delete and base memento simply add `IHasSoftDelete`:
  ```csharp
  public class OrderMemento : MementoBase, IHasSoftDelete { ... }
  ```

### Why Not Include `IHasVersionHistory` in `IMementoBase`?

Same reasoning — version history (snapshots + graveyard) is opt-in per aggregate. Including it in the base would force snapshot infrastructure on all entities.

## Acceptance Criteria

- [x] `IErrorSource` marker interface exists in `Yaf.Domain.Interfaces`
- [x] `Error` record implements `IError` with guard clauses on `Code` and `Message`
- [x] `Error` throws `ArgumentException` for null/empty/whitespace `Code` or `Message`
- [x] `IEncryptable` marker interface exists in `Yaf.Domain.Interfaces`
- [x] `EncryptAttribute` exists in `Yaf.Domain.Attributes` with `AttributeTargets.Property`
- [x] `EncryptAttribute` has `Inherited = true` and `AllowMultiple = false`
- [x] `IMementoBase` combines `IHasIdentity`, `IHasAccountability`, `IHasTimestamps`, `IHasVersionInfo`
- [x] `ITenantMementoBase` extends `IMementoBase` and `IHasTenantId`
- [x] `MementoBase` abstract class implements all `IMementoBase` properties
- [x] `TenantMementoBase` extends `MementoBase` and adds `TenantId` property
- [x] All public types have XML documentation
- [x] Solution builds with zero warnings
- [x] Tests cover:
  - [x] `Error` construction (valid and invalid inputs)
  - [x] `Error` equality (record semantics)
  - [x] `IErrorSource` can be applied to a class declaring `IError` members
  - [x] `EncryptAttribute` can be applied to supported property types
  - [x] `IEncryptable` can be implemented by a memento class
  - [x] `MementoBase` properties are readable/writable
  - [x] `TenantMementoBase` inherits `MementoBase` properties and adds `TenantId`
  - [x] `MementoBase` implements `IMementoBase` (interface satisfaction)
  - [x] `TenantMementoBase` implements `ITenantMementoBase` (interface satisfaction)
  - [x] Round-trip: entity snapshot → `MementoBase`-derived memento → restore (validates integration with existing memento infrastructure)
  - [x] Round-trip with tenant: same as above with `TenantMementoBase`

## Design Decisions

### `Error` is a sealed record, not a class

A `record` gives structural equality (two `Error` instances with the same `Code` and `Message` are equal), which is the right semantic for error values. Consistent with `ValueObject` being a record and `TypedId` being a record. Sealed because there is no reason to inherit from a concrete error.

### `IEncryptable` + `[Encrypt]` — both required (belt-and-suspenders)

`IEncryptable` acts as a gate: infrastructure only scans for `[Encrypt]` properties on mementos implementing `IEncryptable`. This prevents accidental encryption in non-encryption-aware mementos and makes intent explicit. If `[Encrypt]` appears without `IEncryptable`, infrastructure ignores it (or warns at startup — future scope).

### `IErrorSource` is a pure marker — no members

Discovery is reflection-based: scan for types implementing `IErrorSource`, then collect their `static` `IError`-typed properties and fields. A method like `GetErrors()` would force every error source to implement boilerplate — reflection is more ergonomic and consistent with how `IHasVersionHistory` (another marker) works.

### Base memento classes are abstract classes, not records

Mementos are mutable DTOs with `{ get; set; }` properties — required for EF Core materialisation and memento population. Records default to init-only semantics. Abstract classes are the right fit and consistent with `Entity<TId>` / `AggregateRoot<TId>` being classes.

### `IHasSoftDelete` and `IHasVersionHistory` excluded from `IMementoBase`

These are independent opt-in concerns with distinct semantics:
- Soft-delete adds `DeletedAtUtc`/`DeletedBy` columns — most entities don't need them (graveyard is the preferred pattern).
- Version history triggers snapshot infrastructure — performance cost that should be deliberate.

Consumers who need them add the interface directly: `class OrderMemento : MementoBase, IHasSoftDelete { ... }`.

### Single-inheritance trade-off acknowledged

Base memento classes consume the single-inheritance slot. If a consumer's memento must inherit from an ORM base class, they cannot use `MementoBase` — but they can still implement `IMementoBase` directly. The interface exists precisely for this scenario.

### Nullable `[Encrypt]` properties — infrastructure skips nulls

When an encrypted property is `null`, infrastructure skips encryption (no null sentinel). This is the natural and least-surprising behaviour.

### `IHasVersionInfo.Version` defaults to `Guid.Empty`

Non-nullable `Guid` property. The base class initialises it to `default` (which is `Guid.Empty`). Infrastructure overwrites it with a new `Guid` on each save.

### Tenant is NOT auto-mapped by `MementoHelper`

The existing `MementoHelper.HydrateCrossCutting` auto-maps timestamps, accountability, and soft-delete — but not tenant. This is by design: tenant is set at creation time via `ITenantScoped` on the domain entity, and consumers map it manually in `SnapshotCore`/`HydrateCore`. The `TenantMementoBase` provides the property but does not change this mapping convention.

## Dependencies & Risks

**Dependencies:**
- Existing `IError` interface (no changes needed)
- Existing `IHas*` memento interfaces (no changes needed)
- Existing `MementoHelper` and `Entity`/`AggregateRoot` base classes (no changes needed)

**Risks:**
- **Low:** `EncryptAttribute` naming collision — unlikely since `System.Security` doesn't define one. The `Yaf.Domain.Attributes` namespace keeps it unambiguous.
- **Low:** Consumers may forget `IEncryptable` when using `[Encrypt]` — infrastructure validation (future scope) will catch this at startup.

## References & Research

### Internal References
- Brainstorm: [YAF DDD Concepts](../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — sections 12 (Error Hierarchy), 17 (Encryptability), 2 (Memento Pattern)
- Existing `IError`: `src/Yaf.Domain/Interfaces/IError.cs`
- Existing memento interfaces: `src/Yaf.Domain/Interfaces/IHas*.cs`
- Existing `MementoHelper`: `src/Yaf.Domain/Helpers/MementoHelper.cs`
- Previous plan (pattern reference): `docs/plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md`
