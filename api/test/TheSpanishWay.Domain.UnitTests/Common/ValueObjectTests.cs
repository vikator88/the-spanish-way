using Shouldly;
using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.UnitTests.Common;

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

    private sealed class Weight(decimal amount, string currency) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }

    [Fact]
    public void Equal_components_make_equal_values()
    {
        new Money(10m, "EUR").ShouldBe(new Money(10m, "EUR"));
        new Money(10m, "EUR").GetHashCode().ShouldBe(new Money(10m, "EUR").GetHashCode());
    }

    [Fact]
    public void Any_differing_component_breaks_equality()
    {
        new Money(10m, "EUR").ShouldNotBe(new Money(11m, "EUR"));
        new Money(10m, "EUR").ShouldNotBe(new Money(10m, "USD"));
    }

    [Fact]
    public void Different_types_with_identical_components_are_not_equal()
    {
        ValueObject money = new Money(10m, "EUR");
        ValueObject weight = new Weight(10m, "EUR");

        money.ShouldNotBe(weight);
    }

    [Fact]
    public void Value_objects_expose_no_setters()
    {
        var settable = typeof(Money)
            .GetProperties()
            .Where(p => p.SetMethod is not null)
            .Select(p => p.Name)
            .ToList();

        settable.ShouldBeEmpty();
    }
}
