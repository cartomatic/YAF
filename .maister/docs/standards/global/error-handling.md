## Error Handling

### Clear User Messages
Show helpful, actionable messages without exposing internal details or security-sensitive information.

### Fail Fast
Validate inputs and check preconditions early; reject invalid data before it causes deeper issues.

### Typed Exceptions
Use specific exception types instead of generic ones to enable precise error handling.

### Centralized Handling
Catch and process errors at appropriate boundaries (controllers, API layers) rather than scattering try-catch throughout.

### Graceful Degradation
When non-critical services fail, continue operating with reduced functionality rather than crashing entirely.

### Retry with Backoff
Use exponential backoff for transient failures when calling external services.

### Resource Cleanup
Always release resources (file handles, connections) in finally blocks or equivalent cleanup mechanisms.

### Warnings as Errors
`TreatWarningsAsErrors` is enabled in `Directory.Build.props`. Zero-warning builds are required. Any warning fails the build.

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

### Warning Suppression Rules
Use the narrowest scope possible (prefer inline `#pragma`). Always include a justification comment. Never suppress warnings globally.

```csharp
#pragma warning disable CA1062 // Validated by guard clause above
SomeMethod(param);
#pragma warning restore CA1062
```

### Nullable Reference Types
Enabled globally. Use `is null` / `is not null` for null checks. No null-forgiving operator (`!`) without a justification comment explaining why it is safe.

```csharp
// Prefer
if (value is null) throw new ArgumentNullException(nameof(value));

// Avoid
if (value == null) ...
```
