namespace WardMate.SharedKernel.Domain;

/// <summary>
/// Aggregate root — the consistency boundary for a cluster of domain objects.
/// Only aggregate roots should be referenced directly by repositories.
/// </summary>
public abstract class AggregateRoot : BaseEntity
{
    /// <summary>
    /// Optimistic-concurrency row version (auto-incremented by EF Core / Postgres).
    /// Prevents lost-update anomalies without pessimistic locking.
    /// </summary>
    public uint Version { get; private set; }
}
