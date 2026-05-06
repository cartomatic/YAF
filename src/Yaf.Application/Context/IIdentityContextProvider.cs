using Yaf.Domain;

namespace Yaf.Application.Context;

/// <summary>
/// Exposes the ambient actor identifier — who is performing the in-flight
/// operation — for the application pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Infrastructure populates the identity context from authenticated user claims
/// for interactive requests, and from a deterministic system actor for background
/// jobs, scheduled tasks, and message-bus consumers. Pipeline behaviors read this
/// provider to stamp commands, queries, notifications, and persisted entities that
/// implement <see cref="Yaf.Domain.Interfaces.IActorScoped"/> with the originating
/// <see cref="Yaf.Domain.ActorId"/>.
/// </para>
/// <para>
/// <see cref="ActorId"/> is non-nullable by design. Every operation has an
/// accountable actor: when no human user is present, infrastructure substitutes a
/// well-known system actor identifier. This guarantees that downstream auditing,
/// authorization, and event provenance can rely on a value being present without
/// null-checks throughout the pipeline.
/// </para>
/// </remarks>
public interface IIdentityContextProvider
{
    /// <summary>
    /// The identifier of the actor responsible for the current ambient operation.
    /// Always populated — infrastructure substitutes a system actor when no human
    /// user is authenticated.
    /// </summary>
    ActorId ActorId { get; }
}
