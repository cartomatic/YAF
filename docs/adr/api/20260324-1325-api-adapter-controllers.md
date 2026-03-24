# API Adapter — Controllers

- **Timestamp:** 2026-03-24 13:25
- **Status:** under review
- **Scope:** api
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

Yaf.Api provides an opinionated ASP.NET Core controller-based API layer as the first adapter. It includes a base `YafApiController` with automatic ProblemDetails error mapping, global exception handling, API versioning, JWT Bearer auth scaffolding, FluentValidation integration, and health/liveness endpoints. Minimal API support is deferred to a future `Yaf.Api.MinimalApis` package.

## Drivers

1. **Consistent error responses** — Every API error (validation, domain, application, unhandled) should produce a structured ProblemDetails response. No raw strings, no inconsistent formats.
2. **Result-to-HTTP mapping** — The `Result<T>` / `IError` pattern needs a clean translation to HTTP status codes and ProblemDetails bodies.
3. **Enterprise adoption** — Controllers are widely used in enterprise .NET. Tooling (Swagger, testing, code generation) has mature controller support.
4. **Minimal boilerplate** — Consumer controllers should focus on dispatching commands/queries and returning results. Cross-cutting concerns (error handling, validation, auth) should be handled by the framework.
5. **API documentation** — Endpoints should be self-documenting via OpenAPI/Swagger with versioning support.

## Options

### API Style

| Option | Assessment |
|--------|------------|
| **Controllers first** | **Selected.** Mature ecosystem, strong tooling, familiar to enterprise teams. Base controller provides error mapping, validation integration, and consistent responses. |
| Minimal APIs first | Growing but ecosystem (testing patterns, Swagger integration, filters) is still maturing. Better as a separate, optional package. |
| Both simultaneously | Doubles the API surface to maintain from day one. Better to ship one well and add the other later. |

### Error Mapping

| Option | Assessment |
|--------|------------|
| **Automatic `Result<T>` to ProblemDetails mapping in base controller** | **Selected.** Base controller provides helper methods that inspect the `Result<T>` and return the appropriate HTTP response with ProblemDetails body. Consistent across all endpoints. |
| Manual mapping per action | Repetitive. Each controller action would need the same switch on error types. |
| Exception-based mapping only | Misses `Result<T>` failures which don't throw. Would require converting Results back to exceptions. |

### API Versioning

| Option | Assessment |
|--------|------------|
| **Asp.Versioning with URL segment versioning as default** | **Selected.** `/api/v1/orders`. Clear, discoverable, cache-friendly. Asp.Versioning supports header and query string too if consumers prefer. |
| Header-based only | Less discoverable. Harder to test in browser. |
| No versioning | Breaking changes have no migration path. Unacceptable for a framework library. |

### Authentication

| Option | Assessment |
|--------|------------|
| **JWT Bearer configuration helpers** | **Selected.** YAF provides helpers to configure JWT Bearer auth with sensible defaults. Consumer supplies their identity provider details. YAF does not own the identity provider. |
| Built-in identity system | Out of scope. Identity management is application-specific. |
| No auth scaffolding | Consumers wire it up from scratch every time. |

## Recommendation

Controller-based API with base controller for Result-to-ProblemDetails mapping, Asp.Versioning with URL segments, JWT Bearer helpers, and FluentValidation pipeline integration. All configured via `AddYaf()` / `UseYaf()`.

## Consequences

**Positive:**
- Consistent ProblemDetails responses across all endpoints — clients get predictable error shapes
- Base controller eliminates repetitive error mapping code
- API versioning from day one prevents breaking change pain later
- FluentValidation integration validates request DTOs before they reach handlers
- `AddYaf()` / `UseYaf()` gives consumers a production-ready API with minimal setup

**Negative:**
- Controller-based only initially — consumers who prefer Minimal APIs must wait or bypass Yaf.Api
- Opinionated defaults may not suit every consumer (mitigated: configuration options for customization)
- Base controller inheritance couples consumer controllers to YAF (mitigated: thin base, mostly helper methods)

## Conclusion

### Base Controller

`YafApiController` provides:

```
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class YafApiController : ControllerBase
{
    // Result<T> → IActionResult mapping
    protected IActionResult FromResult<T>(Result<T> result);

    // Dispatches command via CQRS pipeline, maps result to response
    protected Task<IActionResult> Command<T>(ICommand<T> command);

    // Dispatches query via CQRS pipeline, maps result to response
    protected Task<IActionResult> Query<T>(IQuery<T> query);
}
```

**Consumer usage:**
```
[ApiVersion("1.0")]
public class OrdersController : YafApiController
{
    [HttpPost]
    public Task<IActionResult> PlaceOrder(PlaceOrderRequest request)
        => Command(new PlaceOrderCommand(request.CustomerId, request.Items));

    [HttpGet("{id}")]
    public Task<IActionResult> GetOrder(Guid id)
        => Query(new GetOrderQuery(new OrderId(id)));
}
```

Consumer controllers are minimal — map HTTP request to command/query, call `Command()` or `Query()`, get back the correct HTTP response automatically.

### Result-to-HTTP Mapping

`FromResult<T>` maps `Result<T>` to HTTP responses:

| Result | Error Type | HTTP Status | Response Body |
|--------|-----------|-------------|---------------|
| Success | — | 200 OK (or 201 Created) | `T` serialized as JSON |
| Failure | `IDomainError` | 422 Unprocessable Entity | ProblemDetails with error code, message, metadata |
| Failure | `NotFoundError` | 404 Not Found | ProblemDetails |
| Failure | `UnauthorizedError` | 403 Forbidden | ProblemDetails |
| Failure | `ConflictError` | 409 Conflict | ProblemDetails |
| Failure | `ValidationError` | 400 Bad Request | ProblemDetails with validation details |
| Failure | Other `IApplicationError` | 422 Unprocessable Entity | ProblemDetails |

### ProblemDetails Format

All error responses follow RFC 9457 (Problem Details for HTTP APIs):

```json
{
  "type": "https://yaf.dev/errors/order-exceeds-limit",
  "title": "Order Exceeds Limit",
  "status": 422,
  "detail": "Order total $12,500 exceeds the $10,000 limit without manager approval.",
  "instance": "/api/v1/orders",
  "errors": {
    "code": "ORDER_EXCEEDS_LIMIT",
    "metadata": { "limit": 10000, "actual": 12500 }
  },
  "traceId": "00-abc123..."
}
```

### Global Exception Handler

Unhandled exceptions (infrastructure failures, programming errors) are caught by middleware and mapped to 500 ProblemDetails:

- No internal details exposed to the client (stack traces, connection strings)
- Exception is logged with full context (via Serilog + correlation ID)
- Response includes `traceId` for support correlation

### FluentValidation Integration

Request DTO validators are auto-discovered and registered by `AddYaf()`. Validation runs before the request reaches the controller action:

- Invalid requests return 400 Bad Request with ProblemDetails containing field-level validation errors
- Controller action is never invoked for invalid requests
- Validators live alongside request DTOs in the consumer's API project

### API Versioning

Configured via Asp.Versioning with `AddYaf()`:

- **Default strategy:** URL segment — `/api/v1/orders`
- **Version format:** major only (v1, v2) — minor versions are backward-compatible
- **Default version:** v1.0 if not specified
- **Sunset headers:** supported for deprecated versions
- Consumer overrides the strategy if they prefer header (`api-version: 1.0`) or query string (`?api-version=1.0`)

### JWT Bearer Auth Scaffolding

`AddYaf()` provides helpers for JWT Bearer configuration:

```
builder.AddYaf(options =>
{
    options.UseJwtBearer(jwtOptions =>
    {
        jwtOptions.Authority = "https://identity.example.com";
        jwtOptions.Audience = "my-api";
    });
});
```

- Configures ASP.NET Core JWT Bearer authentication with sensible defaults
- Consumer provides authority, audience, and any custom claim mappings
- Authorization policies are the consumer's responsibility — YAF provides the auth pipeline, not the policies
- `IIdentityContextProvider` is populated from JWT claims

### Health Endpoints

| Endpoint | Purpose | Checks |
|----------|---------|--------|
| `/health` | Readiness — is the app ready to serve? | Database connectivity, critical dependencies |
| `/alive` | Liveness — is the process alive? | Lightweight, no dependency checks |

Registered automatically by `AddYaf()`. Consumer adds custom health checks for their dependencies.

### OpenAPI / Swagger

`AddYaf()` configures OpenAPI generation with:
- Version-aware documentation (one doc per API version)
- ProblemDetails schema for error responses
- JWT Bearer security scheme
- Error catalog integration (future — expose discovered errors in the OpenAPI spec)

### Middleware Pipeline

`UseYaf()` configures the middleware pipeline in the correct order:

```
app.UseYaf();

// Expands to (conceptually):
app.UseExceptionHandler();     // Global exception → ProblemDetails
app.UseAuthentication();        // JWT Bearer
app.UseAuthorization();         // Consumer policies
app.UseTenantResolution();      // ITenantResolver → ITenantContextProvider
app.UseCorrelationId();         // X-Correlation-Id header → ICorrelationIdProvider
app.UseRequestLogging();        // Serilog request/response logging
app.UseHealthChecks();          // /health and /alive
app.MapControllers();           // Route to controllers
```

Consumer can insert custom middleware before or after `UseYaf()`.

### Future: Minimal APIs (Yaf.Api.MinimalApis)

A separate package providing:
- Endpoint filter for Result-to-ProblemDetails mapping
- Typed result extensions for minimal API handlers
- Same FluentValidation, versioning, and auth integration
- Same error mapping semantics as controllers

Not in initial scope. Controllers are the first-class adapter.

## More Information

- [YAF Library Design Brainstorm](../../brainstorms/20260322-1755-yaf-library-design-brainstorm.md) — controllers first decision, cross-cutting concerns
- [ADR: Technology Stack](../architecture/20260324-0948-technology-stack.md) — FluentValidation, Asp.Versioning, JWT Bearer
- [ADR: Result and Error Pattern](../domain/20260324-1140-result-and-error-pattern.md) — Result\<T\> and IError hierarchy
- [ADR: Validation Strategy](../domain/20260324-1141-validation-strategy.md) — FluentValidation at API boundary
- [ADR: CQRS and Mediator Abstraction](../architecture/20260324-1144-cqrs-and-mediator-abstraction.md) — command/query dispatch
- [ADR: Multi-Tenancy](../infrastructure/20260324-1323-multi-tenancy.md) — tenant resolution middleware
- [ADR: Observability](../infrastructure/20260324-1252-observability.md) — request logging, correlation
