using MediatR;

namespace WardMate.SharedKernel.Domain;

/// <summary>
/// Marker interface for domain events published via MediatR.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>Unique identifier for this event occurrence.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp when the event was created.</summary>
    DateTime OccurredOnUtc { get; }
}
