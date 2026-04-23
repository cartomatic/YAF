# System Architecture

## Overview
YAF implements Domain-Driven Design with Clean Architecture principles. Currently only the domain layer exists; infrastructure and API layers are planned.

## Architecture Pattern
**Pattern**: DDD + Clean Architecture (library, inner layer only)

The domain layer provides framework building blocks that consuming applications compose into their domain models. Dependencies flow inward — the domain layer has zero external dependencies (no NuGet packages in production code).

## System Structure

### Yaf.Domain (Core Library)
- **Location**: `src/Yaf.Domain/`
- **Purpose**: DDD building blocks for business applications
- **Key Components**:

| Component | File(s) | Purpose |
|-----------|---------|---------|
| TypedId | `ITypedId.cs`, `ActorId.cs` | Strongly-typed identifiers (Guid-backed) |
| Entity | `Entity.cs` | Base entity with identity-based equality |
| AggregateRoot | `AggregateRoot.cs` | Consistency boundary with domain events |
| ValueObject | `ValueObject.cs` | Immutable value objects via C# records |
| Error | `Error.cs` | Immutable error with auto-generated codes |
| Result / Result\<T\> | `Result.cs`, `ResultT.cs` | Railway-oriented success/failure outcomes |

### Cross-Cutting Interfaces
- **Location**: `src/Yaf.Domain/Interfaces/`
- **Purpose**: Contracts for entity concerns (audit, timestamps, soft-delete, versioning, tenancy)

| Interface | Purpose |
|-----------|---------|
| IAccountable | Audit trail (CreatedBy, ModifiedBy) |
| IHasTimestamps | Temporal tracking (CreatedAt, ModifiedAt) |
| IHasSoftDelete | Soft delete support (IsDeleted, DeletedAt) |
| IHasVersionInfo | Optimistic concurrency (Version) |
| ITenant | Multi-tenancy (TenantId) |
| IDomainEvent | Domain event marker |
| IErrorSource | Error context |
| IEncryptable | Encryption metadata |

### Memento Pattern
- **Location**: `src/Yaf.Domain/` (MementoBase, MementoBridge) + `Helpers/`
- **Purpose**: State snapshot/hydration for persistence without exposing domain internals
- **Pattern**: Parameterless constructors + RuntimeHelpers.GetUninitializedObject for restoration

### Tests
- **Location**: `tests/Yaf.Domain.Tests/`
- **Purpose**: Unit tests for all building blocks (90+ tests)

## Data Flow
```
[Consumer Application]
    ↓ uses
[Yaf.Domain Building Blocks]
    ↓ produces
[Domain Events] → [Infrastructure dispatches]
    ↓ persists via
[Memento Pattern] → [Infrastructure adapter (planned)]
```

## External Integrations
None — the domain layer is deliberately dependency-free. Infrastructure adapters (EF Core, logging, etc.) will be separate packages.

## Configuration
- **Directory.Build.props** — Shared compiler settings (warnings-as-errors, nullable, XML docs)
- **EditorConfig** — Code style enforcement (90+ rules)
- **GitVersion.yml** — Semantic versioning configuration

## Planned Layers

### Infrastructure Layer (Planned)
- EF Core persistence adapter
- Repository implementations
- Unit of Work pattern
- Domain event publishing infrastructure

### API Layer (Planned)
- ASP.NET Core integration
- Controller/endpoint patterns
- Example web application

---
*Based on codebase analysis performed 2026-04-23*
