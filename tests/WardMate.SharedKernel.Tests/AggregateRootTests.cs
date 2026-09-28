using WardMate.SharedKernel.Domain;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class AggregateRootTests
{
    private sealed class OrderAggregate : AggregateRoot
    {
    }

    [Fact]
    public void NewAggregateRoot_InheritsBaseEntityProperties()
    {
        var aggregate = new OrderAggregate();

        Assert.NotEqual(Guid.Empty, aggregate.Id);
        Assert.False(aggregate.IsDeleted);
        Assert.Equal(0u, aggregate.Version);
        Assert.Empty(aggregate.DomainEvents);
    }
}
