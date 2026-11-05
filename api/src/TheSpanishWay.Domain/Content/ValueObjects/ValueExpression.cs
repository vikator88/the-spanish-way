using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Content.ValueObjects;

/// <summary>
/// How much an effect changes something: either a fixed number or a range to roll.
/// <para>
/// A deliberately tiny grammar. v0.1 allows a "value expression from a restricted supported
/// set", and the restriction is the point — an authored arithmetic string would be an
/// expression evaluator, and an expression evaluator in content is a way to execute code.
/// </para>
/// </summary>
public abstract record ValueExpression
{
    private ValueExpression()
    {
    }

    /// <summary>Always this number.</summary>
    public sealed record Fixed(decimal Value) : ValueExpression;

    /// <summary>A number drawn from an inclusive range when the effect is instantiated.</summary>
    public sealed record Range(decimal Minimum, decimal Maximum) : ValueExpression;

    public static Result<ValueExpression> OfRange(decimal minimum, decimal maximum)
    {
        if (minimum > maximum)
        {
            return Result.Failure<ValueExpression>(
                new Error("value_expression.inverted_range", $"Range [{minimum}, {maximum}] is inverted."));
        }

        return Result.Success<ValueExpression>(new Range(minimum, maximum));
    }

    /// <summary>
    /// Settles on a number, now. Rolling at instantiation rather than at application is what
    /// lets a saved game explain itself: the queued effect carries the value it will apply,
    /// and reloading cannot turn a +1 into a +3.
    /// </summary>
    public decimal Resolve(IRandomSource random, RandomStream stream)
    {
        ArgumentNullException.ThrowIfNull(random);

        return this switch
        {
            Fixed fixedValue => fixedValue.Value,
            Range range => range.Minimum
                + ((range.Maximum - range.Minimum) * random.NextUnitInterval(stream)),
            _ => 0m,
        };
    }
}
