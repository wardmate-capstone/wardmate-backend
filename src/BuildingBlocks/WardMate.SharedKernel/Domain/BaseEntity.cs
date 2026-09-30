namespace WardMate.SharedKernel.Domain;

/// <summary>
/// Base class for all domain entities.
/// Provides identity, soft-delete, audit timestamps, and domain event collection.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Primary key (UUID v4 generated at creation time).</summary>
    public Guid Id { get; protected init; } = Guid.NewGuid();

    /// <summary>UTC timestamp set when the entity is first persisted.</summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp updated on every mutation; null when never updated.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Soft-delete flag. Entities with IsDeleted = true are excluded from queries.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>UTC timestamp recorded when the entity is soft-deleted.</summary>
    public DateTime? DeletedAtUtc { get; private set; }

    // ── Domain events ────────────────────────────────────────────────────────

    /// <summary>Read-only snapshot of pending domain events.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Registers a domain event to be dispatched after the unit-of-work commits.</summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    /// <summary>Clears all pending domain events. Called by the infrastructure after dispatch.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    // ── Audit helpers ────────────────────────────────────────────────────────

    /// <summary>Called by EF Core / repository on insert.</summary>
    public void SetCreated(DateTime utcNow) => CreatedAtUtc = utcNow;

    /// <summary>Called by EF Core / repository on update.</summary>
    public void SetUpdated(DateTime utcNow) => UpdatedAtUtc = utcNow;

    /// <summary>Performs soft-delete instead of physical row removal.</summary>
    public void SoftDelete(DateTime utcNow)
    {
        IsDeleted = true;
        DeletedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }
}
