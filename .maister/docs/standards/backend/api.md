## API Design

### RESTful Principles
Use resource-based URLs with appropriate HTTP methods (GET, POST, PUT, PATCH, DELETE).

### Consistent Naming
Use lowercase, hyphenated or underscored names consistently across endpoints.

### Versioning
Implement versioning (URL path or headers) to manage breaking changes.

### Plural Nouns
Use plural nouns for resources (`/users`, `/products`).

### Limited Nesting
Keep URL nesting to 2-3 levels maximum for readability.

### Query Parameters
Use query parameters for filtering, sorting, and pagination.

### Proper Status Codes
Return appropriate HTTP status codes (200, 201, 400, 404, 500).

### Rate Limit Headers
Include rate limit information in response headers.

### Clean Architecture Layers
Domain (zero deps) -> Application -> Infrastructure/Api (peers). Adapter packages follow the naming convention `Yaf.{Layer}.{Implementation}`.

```
Yaf.Domain          -- zero NuGet dependencies
Yaf.Application     -- depends on Domain
Yaf.Infrastructure  -- depends on Application
Yaf.Api             -- depends on Application
Yaf.Infrastructure.EfCore  -- adapter package
```

### CQRS Abstractions
YAF-owned `ICommand<TResult>`, `IQuery<TResult>`, mediator-agnostic. Adapter packages bridge to specific implementations (e.g., MediatR).

```csharp
public interface ICommand<TResult>;
public interface IQuery<TResult>;
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>;
```

### Sanitization Before Validation
In the CQRS pipeline, sanitization (trimming, normalizing) runs before validation. Validators see clean input.
