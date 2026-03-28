namespace Yaf.Domain.Interfaces;

/// <summary>
/// Marker interface for mementos that participate in version history tracking.
/// When present, infrastructure stores a memento snapshot on each Unit of Work commit
/// and moves deleted aggregates to the graveyard table.
/// </summary>
/// <remarks>
/// <para>
/// This is an independent marker — it does not extend <see cref="IHasVersionInfo"/>.
/// Concurrency and version history are separate concerns that can be used independently
/// or together.
/// </para>
/// <para>
/// Snapshots are tenant-scoped if the aggregate is tenant-scoped.
/// One snapshot per Unit of Work commit (not intermediate saves).
/// The graveyard has its own independent data model.
/// </para>
/// </remarks>
public interface IHasVersionHistory;
