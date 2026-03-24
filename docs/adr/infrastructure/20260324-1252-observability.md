# Observability

- **Timestamp:** 2026-03-24 12:52
- **Status:** under review
- **Scope:** infrastructure
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF provides structured logging (Serilog), distributed tracing and metrics (OpenTelemetry), and health checks (ASP.NET Core) as a cohesive observability stack. Logging and tracing are correlated via OpenTelemetry context and enriched with YAF's context providers (tenant, identity, correlation, activity). The Aspire service defaults package provides a preconfigured observability baseline for consumers who use .NET Aspire.

## Drivers

1. **Production visibility** — Operators need logs, traces, and metrics to understand application behavior, diagnose issues, and monitor health.
2. **Correlation** — A single business operation may span multiple log entries, database calls, and service hops. All telemetry must be correlated via trace/correlation IDs.
3. **Context enrichment** — Logs and traces should carry YAF's context (tenant, identity, correlation, activity) automatically, not manually.
4. **Vendor neutrality** — Telemetry should export to any backend (Jaeger, Zipkin, Prometheus, Azure Monitor, Datadog) without code changes.
5. **Health monitoring** — Load balancers and orchestrators (Kubernetes, Aspire) need health and liveness endpoints to manage service lifecycle.
6. **Framework-provided defaults** — Consumers should get sensible observability out of the box with `AddYaf()`, not wire it up from scratch.

## Options

### Structured Logging

| Option | Assessment |
|--------|------------|
| **Serilog** | **Selected.** De facto standard for structured logging in .NET. Rich sink ecosystem, enrichment pipeline, OpenTelemetry integration. |
| Microsoft.Extensions.Logging alone | Insufficient — no structured sink ecosystem, limited enrichment. Serilog builds on top of it. |
| NLog | Viable but smaller ecosystem in modern .NET. No compelling advantage over Serilog. |

### Distributed Tracing & Metrics

| Option | Assessment |
|--------|------------|
| **OpenTelemetry for .NET** | **Selected.** Vendor-neutral, industry standard. Traces, metrics, and log correlation. Exports to any compatible backend. |
| Application Insights SDK | Azure-specific. Vendor lock-in. |
| Custom tracing | Unnecessary. OpenTelemetry is the industry standard with broad support. |

### Health Checks

| Option | Assessment |
|--------|------------|
| **ASP.NET Core Health Checks** | **Selected.** Framework-native. Rich ecosystem of check packages (EF Core, SQL, Redis, external services). Integrates with Kubernetes probes and Aspire. |
| Custom health endpoints | Reinvents what the framework provides. Misses the ecosystem of existing health check packages. |

## Recommendation

Serilog + OpenTelemetry + ASP.NET Core Health Checks as a unified observability stack, enriched with YAF context providers. Aspire service defaults as an optional accelerator.

## Consequences

**Positive:**
- Logs, traces, and metrics are correlated out of the box — a single correlation ID connects everything
- YAF context (tenant, identity, correlation, activity) automatically enriches all telemetry
- Vendor-neutral — export to any OpenTelemetry-compatible backend without code changes
- Health checks integrate with Kubernetes, Aspire, and load balancers natively
- Consumers get production-ready observability with `AddYaf()` — no manual wiring

**Negative:**
- Serilog is a third-party dependency (mitigated: de facto standard, stays in outer layers)
- OpenTelemetry SDK adds dependencies and some startup overhead (mitigated: standard practice for production services)
- Consumers who prefer a different logging library must override YAF's defaults

## Conclusion

### Three Pillars

| Pillar | Technology | Package | Configuration |
|--------|-----------|---------|---------------|
| **Logging** | Serilog + OpenTelemetry sink | Yaf.Api / Yaf.Infrastructure | `AddYaf()` configures Serilog with structured output and OpenTelemetry correlation |
| **Tracing** | OpenTelemetry .NET SDK | Yaf.Api / Yaf.ServiceDefaults | Auto-instrumentation for ASP.NET Core, EF Core, HttpClient |
| **Metrics** | OpenTelemetry .NET SDK | Yaf.Api / Yaf.ServiceDefaults | ASP.NET Core metrics, custom YAF metrics (command/query duration, event count) |
| **Health** | ASP.NET Core Health Checks | Yaf.Api | `/health` (readiness) and `/alive` (liveness) endpoints |

### Context Enrichment

All telemetry is automatically enriched with YAF's context:

| Context | Log Property | Trace Attribute | Source |
|---------|-------------|----------------|--------|
| Tenant | `TenantId` | `yaf.tenant.id` | `ITenantContextProvider` |
| Identity | `IdentityId` | `yaf.identity.id` | `IIdentityContextProvider` |
| Correlation | `CorrelationId` | Maps to W3C `traceparent` or custom header | `ICorrelationIdProvider` |
| Activity | `ActivityId` | Maps to `Activity.Current.Id` | `IActivityIdProvider` |

Serilog enrichers read from context providers and attach properties to every log entry within the scope of a request. OpenTelemetry baggage propagates context across service boundaries.

### Logging Configuration

`AddYaf()` configures Serilog with:
- **Structured JSON output** — machine-readable, queryable
- **Console sink** — for local development
- **OpenTelemetry sink** — correlates logs with traces
- **Context enrichment** — tenant, identity, correlation, activity on every entry
- **Request logging** — HTTP request/response logging with duration, status code, path
- **Minimum level** — configurable, defaults to `Information` in production, `Debug` in development

Consumers can extend with additional sinks (file, Seq, Elasticsearch, cloud-specific) via standard Serilog configuration.

### Tracing Configuration

OpenTelemetry auto-instrumentation covers:

| Source | What's Traced |
|--------|--------------|
| **ASP.NET Core** | Incoming HTTP requests — method, route, status, duration |
| **EF Core** | Database queries — command text, duration, database name |
| **HttpClient** | Outgoing HTTP calls — URL, method, status, duration |
| **Wolverine** | Command/query dispatch — handler type, duration, result |
| **YAF pipeline** | Validation, sanitization, event dispatch — custom spans |

Custom traces for YAF-specific operations (command handling, event dispatch) use `System.Diagnostics.ActivitySource` following .NET conventions.

### Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `yaf.commands.duration` | Histogram | Command handler execution time |
| `yaf.queries.duration` | Histogram | Query handler execution time |
| `yaf.events.dispatched` | Counter | Domain events dispatched |
| `yaf.validation.failures` | Counter | Validation failures by type |
| ASP.NET Core built-in | Various | Request rate, duration, error rate, active connections |
| EF Core built-in | Various | Query duration, connection pool usage |

### Health Checks

| Endpoint | Purpose | Kubernetes Probe |
|----------|---------|-----------------|
| `/health` | **Readiness** — is the application ready to serve traffic? Checks database connectivity, critical dependencies. | `readinessProbe` |
| `/alive` | **Liveness** — is the application process alive? Lightweight, no dependency checks. | `livenessProbe` |

`AddYaf()` registers both endpoints. Consumers add custom health checks for their dependencies:

```
builder.AddYaf(options =>
{
    options.AddHealthCheck<CustomDependencyCheck>("custom-dependency");
});
```

### Aspire Service Defaults (Yaf.ServiceDefaults)

For consumers using .NET Aspire, `Yaf.ServiceDefaults` provides an additional layer of preconfigured observability:
- OpenTelemetry exporters configured for Aspire dashboard
- Resilience policies (Polly) for HTTP clients
- Service discovery integration
- Enhanced health check UI

This is a **separate, optional package** — `Yaf.Api` does not depend on it.

### What Consumers Get Out of the Box

With just `AddYaf()`:
1. Structured logging with context enrichment — every log entry carries tenant, identity, correlation, activity
2. Distributed tracing across HTTP, EF Core, and the CQRS pipeline
3. Standard metrics for commands, queries, events, and built-in ASP.NET Core metrics
4. Health and liveness endpoints ready for Kubernetes or any orchestrator
5. All telemetry correlated — a single correlation ID connects logs, traces, and metrics for a business operation

No additional configuration needed for local development. Production export targets are configured via standard OpenTelemetry environment variables or Serilog configuration.

## More Information

- [ADR: Technology Stack](../architecture/20260324-0948-technology-stack.md) — Serilog, OpenTelemetry, Aspire selection
- [ADR: Application Layer Patterns](../architecture/20260324-1146-application-layer-patterns.md) — context providers
- [ADR: Solution Structure](../architecture/20260324-0953-solution-structure-and-package-layering.md) — Yaf.ServiceDefaults as separate package
- [YAF Library Design Brainstorm](../../brainstorms/20260322-1755-yaf-library-design-brainstorm.md) — cross-cutting concerns (section "Key Decisions" #4)
