namespace Yaf.Domain.Interfaces;

/// <summary>
/// Carries the identity of the user who triggered the operation.
/// </summary>
/// <remarks>
/// <para>
/// This is semantically distinct from <see cref="IAccountable{TActorId}"/>:
/// <see cref="IAccountable{TActorId}"/> tracks who created/modified an <em>entity</em>
/// (persisted on the entity, set by infrastructure during SaveChanges), while
/// <see cref="IUserScoped"/> identifies who caused an <em>event or operation</em> to occur.
/// </para>
/// <para>
/// The user ID is stored as a plain <see cref="Guid"/> rather than a typed ID because
/// events cross aggregate boundaries where the concrete actor type is not known.
/// Infrastructure populates this from <c>IIdentityContextProvider</c>.
/// </para>
/// <para>
/// This interface is standalone and reusable — domain events, commands, queries,
/// and integration events can all implement it.
/// </para>
/// </remarks>
public interface IUserScoped
{
    /// <summary>
    /// The identity of the user who triggered this operation.
    /// </summary>
    Guid UserId { get; }
}
