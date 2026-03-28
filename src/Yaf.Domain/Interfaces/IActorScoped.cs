namespace Yaf.Domain.Interfaces;

/// <summary>
/// Carries the identity of the actor who triggered the operation.
/// </summary>
/// <remarks>
/// <para>
/// This is semantically distinct from <see cref="IAccountable"/>:
/// <see cref="IAccountable"/> tracks who created/modified an <em>entity</em>
/// (persisted on the entity, set by infrastructure during SaveChanges), while
/// <see cref="IActorScoped"/> identifies who caused an <em>event or operation</em> to occur.
/// </para>
/// <para>
/// Uses the framework-provided <see cref="Yaf.Domain.ActorId"/> type, which covers
/// human users, service accounts, and automation bots.
/// Infrastructure populates this from <c>IIdentityContextProvider</c>.
/// </para>
/// <para>
/// This interface is standalone and reusable — domain events, commands, queries,
/// and integration events can all implement it.
/// </para>
/// </remarks>
public interface IActorScoped
{
    /// <summary>
    /// The identity of the actor who triggered this operation.
    /// </summary>
    ActorId ActorId { get; }
}
