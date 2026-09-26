using WardMate.SharedKernel.Domain;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class ValueObjectTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        public decimal Amount { get; } = amount;
        public string Currency { get; } = currency;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void EqualValues_AreEqual()
    {
        var m1 = new Money(100m, "VND");
        var m2 = new Money(100m, "VND");

        Assert.Equal(m1, m2);
        Assert.True(m1 == m2);
        Assert.False(m1 != m2);
        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
    }

    [Fact]
    public void DifferentValues_AreNotEqual()
    {
        var m1 = new Money(100m, "VND");
        var m2 = new Money(200m, "VND");
        var m3 = new Money(100m, "USD");

        Assert.NotEqual(m1, m2);
        Assert.NotEqual(m1, m3);
        Assert.True(m1 != m2);
    }

    [Fact]
    public void NullComparison_ReturnsCorrectResult()
    {
        var m1 = new Money(100m, "VND");
        Money? nullMoney = null;

        Assert.False(m1.Equals(null));
        Assert.False(m1 == nullMoney);
        Assert.True(m1 != nullMoney);
        Assert.True(nullMoney == null);
    }
}
