using WardMate.SharedKernel.Domain;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class BaseEntityTests
{
    private sealed record TestDomainEvent(string Message) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
    }

    private sealed class TestEntity : BaseEntity
    {
        public void DoSomething(string message)
        {
            RaiseDomainEvent(new TestDomainEvent(message));
        }
    }

    [Fact]
    public void NewEntity_InitializesDefaults()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.True(entity.CreatedAtUtc <= DateTime.UtcNow);
        Assert.Null(entity.UpdatedAtUtc);
        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedAtUtc);
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void RaiseDomainEvent_AddsToCollection()
    {
        var entity = new TestEntity();
        entity.DoSomething("event 1");

        Assert.Single(entity.DomainEvents);
        Assert.Equal("event 1", ((TestDomainEvent)entity.DomainEvents[0]).Message);
    }

    [Fact]
    public void ClearDomainEvents_EmptiesCollection()
    {
        var entity = new TestEntity();
        entity.DoSomething("event 1");
        entity.DoSomething("event 2");
        Assert.Equal(2, entity.DomainEvents.Count);

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void SoftDelete_SetsFlagsAndTimestamps()
    {
        var entity = new TestEntity();
        var deleteTime = DateTime.UtcNow;

        entity.SoftDelete(deleteTime);

        Assert.True(entity.IsDeleted);
        Assert.Equal(deleteTime, entity.DeletedAtUtc);
        Assert.Equal(deleteTime, entity.UpdatedAtUtc);
    }

    [Fact]
    public void SetUpdated_UpdatesTimestamp()
    {
        var entity = new TestEntity();
        var updateTime = DateTime.UtcNow;

        entity.SetUpdated(updateTime);

        Assert.Equal(updateTime, entity.UpdatedAtUtc);
    }
}
