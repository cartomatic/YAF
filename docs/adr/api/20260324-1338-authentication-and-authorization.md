# Authentication and Authorization

- **Timestamp:** 2026-03-24 13:38
- **Status:** under review
- **Scope:** api
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF enforces authentication by default on all API endpoints. Anonymous access requires an explicit `[AllowAnonymous]` attribute. Identity is resolved at the API boundary from JWT tokens, short tokens (masking JWT), or API keys — all resolving to the same identity model so accountability is consistent regardless of authentication method. Authorization is privilege-based: the application layer declares privileges, controllers declare requirements via attributes supporting AND/OR composition, and authorization handlers verify privileges before the controller method executes.

## Drivers

1. **Secure by default** — Unauthenticated access should be the exception, not the rule. Forgetting to add an `[Authorize]` attribute should not create a security hole.
2. **Full accountability** — Every operation must be traceable to an identity, including machine-to-machine (M2M) interactions via API keys. Anonymous operations are explicitly opted in.
3. **Flexible authentication** — Different clients authenticate differently: SPAs use JWT tokens, mobile apps may use short tokens, and services use API keys. All must resolve to the same identity model.
4. **Declarative authorization** — Controllers declare what privileges are required, not how to check them. Authorization logic lives in handlers, not in controller code.
5. **Composable requirements** — Real-world authorization needs AND/OR logic: "user must have CanViewOrders AND (IsManager OR IsAdmin)".

## Options

### Authentication Default

| Option | Assessment |
|--------|------------|
| **Authenticated by default, `[AllowAnonymous]` to opt out** | **Selected.** Secure by default. New endpoints are automatically protected. Anonymous access is an explicit, visible decision. |
| Unauthenticated by default, `[Authorize]` to opt in | Insecure by default. Forgetting an attribute creates a security hole. |

### Identity Resolution

| Option | Assessment |
|--------|------------|
| **Multi-scheme resolution to a unified identity model** | **Selected.** JWT, short token, and API key all resolve to the same `IIdentityContextProvider` output — a principal with an identity ID and a set of privileges. Authentication method is transparent to application code. |
| JWT only | Excludes M2M (API key) and short token scenarios. |
| Separate identity models per scheme | Application code must handle different identity shapes. Breaks uniform accountability. |

### Authorization Model

| Option | Assessment |
|--------|------------|
| **Privilege-based with AND/OR composable requirements** | **Selected.** Application layer declares privileges as first-class concepts. Controllers declare requirements via attributes. Requirements support AND (all must match), OR (any must match), and mixed composition. Authorization handlers evaluate requirements against the current identity's privileges before the controller method executes. |
| Role-based only | Too coarse. "Admin" vs "User" doesn't express fine-grained business permissions. |
| Policy-based (ASP.NET Core policies) | Building block for the implementation, but raw policies lack the composable AND/OR attribute syntax. YAF's privilege system builds on top of ASP.NET Core authorization. |
| Permission checks inside handlers | Scattered, inconsistent, hard to audit. Authorization should be declarative and enforced before business logic. |

### API Key Identity

| Option | Assessment |
|--------|------------|
| **API keys map to an identity with privileges** | **Selected.** API keys are not just access tokens — they resolve to a full identity (service account) with assigned privileges. M2M calls have the same accountability as user calls. |
| API keys as opaque tokens | No accountability. Cannot trace M2M operations to a specific service or actor. |
| API keys with separate permission model | Two authorization models to maintain. Breaks uniform privilege checking. |

## Recommendation

Authenticated by default with multi-scheme identity resolution (JWT, short token, API key) to a unified identity. Privilege-based authorization with AND/OR composable attributes. All enforced before controller method execution via ASP.NET Core authorization handlers.

## Consequences

**Positive:**
- Secure by default — no accidental unauthenticated endpoints
- Full accountability for all operations including M2M — API keys resolve to identities
- Declarative authorization — privileges and requirements are visible on controllers, not buried in code
- AND/OR composition handles real-world authorization complexity
- Single identity model regardless of authentication method — application code doesn't care how the user authenticated
- Authorization executes before the controller method — invalid requests never reach business logic

**Negative:**
- Multi-scheme authentication adds configuration complexity (mitigated: YAF provides helpers for each scheme)
- API key → identity mapping requires a lookup mechanism (database, cache) — consumer must implement
- AND/OR composable requirements need custom attributes and authorization handlers beyond vanilla ASP.NET Core
- `[AllowAnonymous]` must be used deliberately — developers must understand the secure-by-default model

## Conclusion

### Authentication — Secure by Default

```csharp
// Yaf.Api registers a global authorization filter
// All endpoints require authentication unless explicitly opted out
builder.AddYaf(options =>
{
    options.RequireAuthenticatedUsers(); // default — applied globally
});
```

Anonymous access is explicit:
```csharp
[AllowAnonymous]
[HttpGet("public/status")]
public IActionResult GetPublicStatus() => Ok("running");
```

### Identity Resolution — Multi-Scheme

Three authentication schemes resolve to the same identity model:

| Scheme | Source | Resolution |
|--------|--------|-----------|
| **JWT Bearer** | `Authorization: Bearer <jwt>` header | Standard JWT validation. Claims mapped to identity. Privileges extracted from claims or fetched from privilege store. |
| **Short Token** | `Authorization: Bearer <short-token>` header | Short token is resolved to the underlying JWT (or identity) via a token exchange service. Same identity output as JWT. |
| **API Key** | `X-Api-Key: <key>` header | API key is looked up in a key store. Maps to a service account identity with assigned privileges. Full accountability for M2M. |

All three schemes populate `IIdentityContextProvider` with:

```
// Resolved identity — same shape regardless of auth method
public interface IIdentityContextProvider
{
    IdentityId CurrentIdentityId { get; }
    AuthenticationScheme Scheme { get; }  // JWT, ShortToken, ApiKey
    IReadOnlySet<GrantedPrivilege> Privileges { get; }
}
```

The identity is resolved and enriched with privileges **early in the middleware pipeline**, before any authorization or controller execution.

### Tenant-Scoped Privileges

Users can be related to multiple tenants with different roles and privileges in each. Privileges are granted **within a tenant scope**. When a user declares a tenant context on a request, their privilege set is filtered to that tenant's grants.

- **Tenant-scoped endpoints** — the user specifies a tenant (via header, JWT claim, etc.). Privileges are resolved for that specific tenant. Authorization checks run against the tenant-scoped privilege set.
- **Tenant-independent endpoints** — some endpoints don't require tenant context (e.g., "list my tenants", "user profile"). These endpoints require authentication but not tenant resolution. Privilege checks on these endpoints use tenant-independent privileges (if any).

**Example — user across multiple tenants:**
```
User "alice" belongs to:
  - Tenant A: roles [owner]        → privileges: [orders:*, users:*, settings:*]
  - Tenant B: roles [external-user] → privileges: [orders:view]

Request to Tenant A → alice has full order management
Request to Tenant B → alice can only view orders
Request without tenant → alice can access tenant-independent endpoints only
```

### Privilege Model — Abstract Structure

At this stage, the privilege model is abstract. The key concepts are:

| Concept | Description |
|---------|-------------|
| **Privilege** | A named permission (e.g., `orders:view`, `orders:create`) |
| **Privilege level** | Optional granularity within a privilege (e.g., `read`, `write`, `admin`) |
| **Granted privilege** | A privilege assigned to an identity within a tenant scope: `(TenantId, IdentityId, Privilege, Level?)` |
| **Privilege set** | The collection of granted privileges for an identity, filtered by the current tenant context |

```csharp
// Yaf.Application — abstract privilege concepts

public abstract record Privilege(string Code);

public record GrantedPrivilege(
    TenantId? TenantId,    // null = tenant-independent
    IdentityId IdentityId,
    Privilege Privilege,
    PrivilegeLevel? Level   // optional granularity
);
```

The consumer defines:
- **What privileges exist** (application-specific declarations)
- **How privileges are stored and retrieved** (`IPrivilegeStore` implementation)
- **How privilege sets are resolved** from tokens, database, or external identity provider
- **What privilege levels mean** for their domain

```csharp
// Consumer declares their privileges
public static class OrderPrivileges
{
    public static readonly Privilege ViewOrders = new("orders:view");
    public static readonly Privilege CreateOrders = new("orders:create");
    public static readonly Privilege ApproveOrders = new("orders:approve");
    public static readonly Privilege ManageAllOrders = new("orders:manage-all");
}
```

### Privilege Resolution Flow

```
Identity resolved (JWT / short token / API key)
  → Tenant context resolved (if applicable)
    → IPrivilegeStore.GetPrivilegesAsync(identityId, tenantId?)
      → Returns granted privileges for this identity + tenant scope
        → Populates IIdentityContextProvider.Privileges
          → Authorization handlers evaluate against this set
```

When no tenant is specified, the privilege store returns only tenant-independent privileges. The authorization handler checks requirements against whatever privilege set was resolved.

### Authorization — Composable Requirements

Controllers declare requirements via attributes:

**Single privilege:**
```csharp
[RequirePrivilege("orders:view")]
[HttpGet]
public Task<IActionResult> ListOrders() => Query(new ListOrdersQuery());
```

**AND — all required:**
```csharp
[RequireAllPrivileges("orders:create", "orders:approve")]
[HttpPost("approved")]
public Task<IActionResult> CreateApprovedOrder(CreateOrderRequest request)
    => Command(new CreateApprovedOrderCommand(request));
```

**OR — any sufficient:**
```csharp
[RequireAnyPrivilege("orders:manage-all", "orders:approve")]
[HttpPost("{id}/approve")]
public Task<IActionResult> ApproveOrder(Guid id)
    => Command(new ApproveOrderCommand(new OrderId(id)));
```

**Mixed AND/OR composition:**
```csharp
// Must have orders:view AND (be a manager OR admin)
[RequirePrivilege("orders:view")]
[RequireAnyPrivilege("role:manager", "role:admin")]
[HttpGet("sensitive")]
public Task<IActionResult> ListSensitiveOrders()
    => Query(new ListSensitiveOrdersQuery());
```

Multiple attributes on the same method compose as AND — all attribute requirements must be satisfied. Within `RequireAnyPrivilege`, the listed privileges compose as OR.

### Authorization Handlers

Authorization is enforced via ASP.NET Core's `IAuthorizationHandler` mechanism:

```
Request arrives
  → Authentication middleware (resolve identity from JWT / short token / API key)
    → Enrich identity with privileges (from claims, privilege store, or key mapping)
      → Authorization middleware (evaluate RequirePrivilege attributes)
        → Authorization handler checks identity.Privileges against requirements
          ✗ → 403 Forbidden (ProblemDetails)
          ✓ → Controller method executes
```

YAF provides the authorization handler that evaluates privilege requirements. Consumer provides:
- **Privilege assignment** — how privileges map to identities (claims in JWT, database lookup, API key configuration)
- **Privilege store** (optional) — `IPrivilegeStore` interface for fetching privileges if not embedded in the token

```csharp
// Yaf.Application
public interface IPrivilegeStore
{
    Task<IReadOnlySet<GrantedPrivilege>> GetPrivilegesAsync(
        IdentityId identityId, TenantId? tenantId = null, CancellationToken ct = default);
}
```

When `tenantId` is provided, returns privileges scoped to that tenant. When null, returns tenant-independent privileges only.

### API Key Configuration

API keys map to service account identities:

```csharp
builder.AddYaf(options =>
{
    options.UseApiKeyAuthentication(apiKey =>
    {
        apiKey.HeaderName = "X-Api-Key";        // default
        apiKey.KeyResolver = services =>
            services.GetRequiredService<IApiKeyResolver>();
    });
});

// Consumer implements
public interface IApiKeyResolver
{
    Task<ApiKeyIdentity?> ResolveAsync(string apiKey, CancellationToken ct = default);
}

public record ApiKeyIdentity(
    IdentityId IdentityId,
    string ServiceName,
    IReadOnlySet<Privilege> Privileges);
```

API keys resolve to a full identity — `IIdentityContextProvider` is populated the same way as for JWT. Service name is logged for observability. Accountability fields (`CreatedBy`, `ModifiedBy`) trace back to the service account identity.

### Short Token Support

Short tokens mask the underlying JWT for scenarios where full JWT transmission is impractical (URL parameters, limited-length headers, QR codes):

```csharp
builder.AddYaf(options =>
{
    options.UseShortTokenAuthentication(shortToken =>
    {
        shortToken.TokenResolver = services =>
            services.GetRequiredService<IShortTokenResolver>();
    });
});

// Consumer implements
public interface IShortTokenResolver
{
    Task<ClaimsPrincipal?> ResolveAsync(string shortToken, CancellationToken ct = default);
}
```

The resolver exchanges the short token for the identity (from a cache, token store, or token exchange endpoint). The resulting identity is indistinguishable from a JWT-authenticated identity.

### Identity Enrichment Pipeline

```
Raw credentials (JWT / short token / API key)
  → Scheme-specific authentication handler
    → Resolve to ClaimsPrincipal / identity
      → Privilege enrichment (from claims, IPrivilegeStore, or API key config)
        → Populate IIdentityContextProvider
          → Available to: authorization handlers, application handlers,
             context envelope, accountability, observability
```

### What Consumer Provides

| Concern | Consumer Responsibility |
|---------|----------------------|
| **JWT configuration** | Authority, audience, claim mappings |
| **Short token resolver** | `IShortTokenResolver` — exchange short token for identity |
| **API key resolver** | `IApiKeyResolver` — look up key, return identity + privileges |
| **Privilege assignment** | How privileges map to identities (claims, database, config) |
| **Privilege store** (optional) | `IPrivilegeStore` if privileges aren't embedded in tokens |
| **Privilege declarations** | Application-specific privilege definitions |
| **Authorization requirements** | `[RequirePrivilege]` attributes on controllers/methods |

### What YAF Provides

| Concern | YAF Responsibility |
|---------|-------------------|
| **Authentication pipeline** | Multi-scheme middleware, identity resolution, privilege enrichment |
| **Authorization attributes** | `[RequirePrivilege]`, `[RequireAllPrivileges]`, `[RequireAnyPrivilege]` |
| **Authorization handler** | Evaluates privilege requirements against identity |
| **Secure-by-default** | Global authenticated-users requirement |
| **Identity context** | `IIdentityContextProvider` populated for all schemes |
| **ProblemDetails mapping** | 401 Unauthorized / 403 Forbidden responses |

## More Information

- [ADR: API Adapter — Controllers](20260324-1325-api-adapter-controllers.md) — controller base, middleware pipeline
- [ADR: Application Layer Patterns](../architecture/20260324-1146-application-layer-patterns.md) — IIdentityContextProvider, context propagation
- [ADR: Cross-Cutting Infrastructure](../infrastructure/20260324-1249-cross-cutting-infrastructure.md) — accountability auto-population from identity
- [ADR: Observability](../infrastructure/20260324-1252-observability.md) — identity enrichment in logs and traces
- [ADR: Multi-Tenancy](../infrastructure/20260324-1323-multi-tenancy.md) — tenant resolved alongside identity
