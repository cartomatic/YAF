# OpenAPI Documentation

- **Timestamp:** 2026-03-24 13:38
- **Status:** under review
- **Scope:** api
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF API adapters generate OpenAPI (Swagger) specifications and provide a browsable UI for API exploration and testing. The OpenAPI spec is version-aware, includes ProblemDetails schemas for error responses, documents authentication requirements, and can be extended with the error catalog. Consumers choose between Swagger UI and Scalar as the documentation frontend.

## Drivers

1. **API discoverability** — API consumers (frontend developers, integrators, third parties) need a browsable, testable API specification without reading source code.
2. **Contract-first potential** — OpenAPI specs enable client SDK generation, contract testing, and API gateway configuration.
3. **Version documentation** — Versioned APIs need per-version OpenAPI documents so consumers can see what's available in each version.
4. **Error documentation** — ProblemDetails error shapes and the error catalog should be visible in the API spec.
5. **Developer experience** — A built-in API explorer reduces onboarding friction for new consumers.

## Options

### OpenAPI Generation

| Option | Assessment |
|--------|------------|
| **ASP.NET Core built-in OpenAPI (Microsoft.AspNetCore.OpenApi)** | **Selected.** .NET 10 has first-class OpenAPI support via `Microsoft.AspNetCore.OpenApi`. Native integration, no third-party dependency for spec generation. |
| Swashbuckle | Long-standing library but maintenance has been inconsistent. Being replaced by Microsoft's built-in support. |
| NSwag | Capable but adds a large dependency. Less necessary now that .NET has built-in support. |

### Documentation UI

| Option | Assessment |
|--------|------------|
| **Scalar as default, Swagger UI as alternative** | **Selected.** Scalar provides a modern, clean API explorer with better UX than Swagger UI. Both consume the same OpenAPI spec. Consumer can switch via configuration. |
| Swagger UI only | Functional but dated UX. Still widely recognized. |
| No UI — spec only | Misses the developer experience benefit. Consumers would need external tools to explore the API. |

## Recommendation

ASP.NET Core built-in OpenAPI generation with Scalar as the default documentation UI. Swagger UI available as an alternative via configuration.

## Consequences

**Positive:**
- API is self-documenting — consumers explore endpoints, schemas, and error shapes without external docs
- Version-aware documentation — each API version gets its own spec
- ProblemDetails schemas in the spec make error handling predictable for consumers
- Scalar provides a modern, polished developer experience
- OpenAPI spec enables client SDK generation and contract testing

**Negative:**
- Two UI options (Scalar + Swagger UI) means two dependencies to maintain (mitigated: both are lightweight, consumer picks one)
- OpenAPI spec generation adds startup overhead (mitigated: negligible, can be disabled in production if needed)
- Built-in OpenAPI in .NET 10 is newer — some edge cases may be less polished than Swashbuckle (mitigated: actively developed by Microsoft)

## Conclusion

### Configuration

```csharp
builder.AddYaf(options =>
{
    options.UseOpenApi(openApi =>
    {
        openApi.Title = "My API";
        openApi.Description = "My application API";
        openApi.UseScalar();        // default UI
        // or: openApi.UseSwaggerUi();
    });
});
```

### OpenAPI Spec Features

| Feature | Description |
|---------|-------------|
| **Per-version documents** | Each API version (`v1`, `v2`) gets its own OpenAPI document |
| **ProblemDetails schemas** | Error responses reference RFC 9457 ProblemDetails schema with YAF extensions (error code, metadata) |
| **Authentication** | JWT Bearer security scheme documented with scopes |
| **Error catalog integration** | Discovered errors from `IErrorSource` can be included as documented response types per endpoint (future enhancement) |
| **XML doc integration** | Controller and DTO XML documentation comments appear as descriptions in the spec |

### Middleware

`UseYaf()` registers the OpenAPI endpoint and documentation UI:

- `/openapi/v1.json` — OpenAPI spec for v1
- `/scalar/v1` or `/swagger` — browsable UI (configurable)
- Documentation endpoints are enabled by default in Development environment
- Consumer controls whether they're exposed in production via configuration

### Environment Defaults

| Environment | OpenAPI Spec | Documentation UI |
|-------------|-------------|-----------------|
| Development | Enabled | Enabled |
| Production | Enabled (for API consumers) | Disabled by default (consumer can enable) |

Consumer overrides via configuration:
```csharp
openApi.ExposeInProduction = true;   // spec
openApi.ExposeUiInProduction = true; // UI
```

## More Information

- [ADR: API Adapter — Controllers](20260324-1325-api-adapter-controllers.md) — ProblemDetails, versioning
- [ADR: Result and Error Pattern](../domain/20260324-1140-result-and-error-pattern.md) — error catalog discovery
- [ADR: Public API Documentation](../architecture/20260324-1338-public-api-documentation.md) — XML docs feed into OpenAPI descriptions
