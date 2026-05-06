using Yaf.Domain;

namespace Yaf.Application.Context;

/// <summary>
/// Exposes the ambient tenant identifier for the in-flight operation, sourced
/// from the host's request, message, or background-job context.
/// </summary>
/// <remarks>
/// <para>
/// Infrastructure populates the tenant context from request headers, claims, or
/// message metadata. Application services and pipeline behaviors read this provider
/// to enforce tenant isolation and to stamp persisted entities that implement
/// <see cref="Yaf.Domain.Interfaces.ITenantScoped"/> with the correct
/// <see cref="Yaf.Domain.TenantId"/>.
/// </para>
/// <para>
/// <see cref="TenantId"/> is nullable because not every operation runs inside a
/// tenant scope. Cross-tenant administration, tenant provisioning and deprovisioning,
/// and platform-level system jobs legitimately execute without a tenant. Consumers
/// that require a tenant must guard against <see langword="null"/> explicitly rather
/// than relying on a default value, so the absence of a tenant is never silently
/// reinterpreted as a real one.
/// </para>
/// </remarks>
public interface ITenantContextProvider
{
    /// <summary>
    /// The tenant identifier for the current ambient context, or <see langword="null"/>
    /// when the operation is not scoped to a tenant (for example, system administration
    /// or tenant lifecycle management).
    /// </summary>
    TenantId? TenantId { get; }
}
