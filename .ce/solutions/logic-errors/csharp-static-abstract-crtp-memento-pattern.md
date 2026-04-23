---
title: "CRTP + static abstract interfaces + GetUninitializedObject for DDD memento pattern"
date: 2026-03-25
category: logic-errors
tags: [csharp, static-abstract, crtp, records, ddd, value-objects, memento-pattern, net10]
component: Yaf.Domain
severity: medium
---

# CRTP + Static Abstract + Memento Pattern for DDD Base Types

## Problem Summary

Building a `ValueObject<TSelf, TMemento>` base record for a DDD framework required combining three C# features: CRTP for type-safe static factory methods, `static abstract` interface members for the `IMemento` contract, and `RuntimeHelpers.GetUninitializedObject` for bypassing constructors on immutable records during memento restoration.

Multiple incorrect assumptions were made during development about C# limitations. This document captures what actually works and what doesn't.

## Key Finding: What Actually Works

**A concrete static method on an abstract class DOES satisfy a `static abstract` interface member.** This was incorrectly assumed to be impossible during development. The following compiles and works correctly in .NET 10:

```csharp
public abstract record ValueObject<TSelf, TMemento> : ValueObject, IMemento<TSelf, TMemento>
    where TSelf : ValueObject<TSelf, TMemento>
    where TMemento : class
{
    public void Snapshot(TMemento memento) { /* ... */ }

    // Concrete static method satisfies IMemento's static abstract Restore
    public static TSelf Restore(TMemento memento)
    {
        var instance = (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));
        instance.RestoreCore(memento);
        // validate...
        return instance;
    }
}
```

Consumers only need:
```csharp
public record Address : ValueObject<Address, IAddressMemento>
{
    // No need to declare : IMemento<Address, IAddressMemento>
    // No need for explicit interface implementation
}
```

## What Actually Doesn't Work

### 1. CS8920: Interface with `static abstract` as generic type argument

Using `IMemento<T, M>` as a generic type argument in certain contexts fails:

```csharp
// CS8920 — won't compile:
Assert.IsAssignableFrom<IMemento<MementoColor, IColorMemento>>(color);

// Workaround — use is pattern:
(color is IMemento<MementoColor, IColorMemento>).Should().BeTrue();
```

### 2. Immutable records + memento restoration

Records with positional parameters or `init` properties can't be restored via `GetUninitializedObject`:

```csharp
// Won't work — init can't be set after GetUninitializedObject:
public int R { get; init; }

// Won't work — positional params generate init-only:
public record Address(string Street) : ValueObject<Address, IAddressMemento>;

// Works — private set allows mutation from RestoreCore:
public int R { get; private set; }
```

### 3. Field initializers bypassed

`GetUninitializedObject` does not run field initializers:

```csharp
// This will be null after GetUninitializedObject, not an empty list:
private readonly List<string> _tags = new();

// Use lazy initialization instead:
private List<string>? _tags;
protected List<string> Tags => _tags ??= new();
```

## Investigation Trail: What We Incorrectly Assumed

During development, we believed abstract classes **cannot** satisfy `static abstract` interface members. This led to several workarounds:

1. Removed `IMemento` from the base class, had consumers declare it
2. Added explicit interface implementations (`static T IMemento<T,M>.Restore(...)`)
3. Spent time on `new static` shadowing approaches

**All of this was unnecessary.** The concrete static `Restore` on the abstract base class satisfies the interface just fine. The errors we encountered (CS8920) were specifically about using the interface as a generic type argument in test assertions — not about the class implementing the interface.

**Lesson:** When C# gives a compilation error, read the error code carefully. CS8920 is about generic type arguments, not about class-level interface implementation. We conflated the two.

## Working Solution

The final pattern:

1. `IMemento<TSelf, TMemento>` — interface with `Snapshot` + `static abstract Restore`
2. `IHydratable<TMemento>` — separate interface for mutable entities (opt-in)
3. `ValueObject<TSelf, TMemento> : ValueObject, IMemento<TSelf, TMemento>` — base implements the interface directly
4. `Restore` uses `GetUninitializedObject` → `RestoreCore` → `Validate`
5. `Validate()` returns `IReadOnlyCollection<IError>`, throws `ValidationException` on failure
6. Template methods named `*Core` (not `*Internal`) per .NET conventions

## Prevention Strategies

- **Read compiler error codes precisely.** CS0535 (doesn't implement member) is different from CS8920 (can't use as type argument). Don't generalize one error to mean a broader limitation.
- **Try the simplest approach first.** We should have tried `abstract class : IInterface` with a concrete static method before assuming it wouldn't work.
- **Use `{ get; private set; }` on all memento-capable types.** Document this prominently.
- **Use `??=` lazy initialization** for any field that must be non-null — `GetUninitializedObject` skips all initializers.
- **Add `Validate()` hook after restoration** — constructors are bypassed, so validation must happen explicitly.

## Testing Recommendations

1. **Round-trip test** — create → snapshot → restore → assert equality
2. **Validation test** — restore with invalid memento → assert `ValidationException`
3. **Use `is` pattern** for interface type checks, not `IsAssignableFrom<T>` (avoids CS8920)
4. **Test restored object behavior** — invoke methods after restore, verify no `NullReferenceException`

## Related Files

- `src/Yaf.Domain/Interfaces/IMemento.cs`
- `src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs`
- `src/Yaf.Domain/ValidationException.cs`
- `docs/adr/domain/20260324-1104-state-management-memento-pattern.md`
