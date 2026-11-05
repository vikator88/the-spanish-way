using System.Numerics;
using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.Shared.ValueObjects;

/// <summary>
/// The result of writing to a <see cref="Bounded{T}"/>: the new value, plus however much
/// of the attempted change the bounds refused.
/// <para>
/// Truncation is reported rather than discarded because the effect engine needs to tell
/// the player "that effect was truncated by 1.5 at the upper bound" instead of silently
/// losing it. See domain/effect-engine, "Clamping is a diagnostic".
/// </para>
/// </summary>
public readonly record struct BoundedChange<T>(Bounded<T> Result, T Truncated)
    where T : struct, INumber<T>, IMinMaxValue<T>
{
    /// <summary>True when the bounds refused part of the change.</summary>
    public bool WasTruncated => Truncated != T.Zero;
}

/// <summary>
/// A number that cannot leave its range. Every bounded game attribute — cohesion,
/// credibility, trust, inflation — uses this, so "no effect may push a variable outside
/// its bounds" is enforced by the type rather than remembered at each call site.
/// </summary>
public readonly record struct Bounded<T>
    where T : struct, INumber<T>, IMinMaxValue<T>
{
    internal Bounded(T value, T minimum, T maximum)
    {
        Value = value;
        Minimum = minimum;
        Maximum = maximum;
    }

    public T Value { get; }

    public T Minimum { get; }

    public T Maximum { get; }

    /// <summary>Replaces the value, clamping to the existing bounds and reporting any truncation.</summary>
    public BoundedChange<T> With(T newValue)
    {
        var clamped = Clamp(newValue, Minimum, Maximum);

        return new BoundedChange<T>(
            new Bounded<T>(clamped, Minimum, Maximum),
            newValue - clamped);
    }

    /// <summary>Adds a delta, clamping to bounds and reporting the part that did not fit.</summary>
    public BoundedChange<T> Add(T delta) => With(Value + delta);

    public BoundedChange<T> Subtract(T delta) => With(Value - delta);

    /// <summary>Multiplies the value, clamping to bounds and reporting the part that did not fit.</summary>
    public BoundedChange<T> Multiply(T factor) => With(Value * factor);

    public bool IsAtMinimum => Value == Minimum;

    public bool IsAtMaximum => Value == Maximum;

    internal static T Clamp(T value, T minimum, T maximum) =>
        value < minimum ? minimum
        : value > maximum ? maximum
        : value;

    public override string ToString() => $"{Value} [{Minimum}..{Maximum}]";
}

/// <summary>
/// Factories for <see cref="Bounded{T}"/>. They live on a non-generic companion so callers
/// write <c>Bounded.Percentage(50m)</c> and let inference do the rest.
/// </summary>
public static class Bounded
{
    public static Result<Bounded<T>> Create<T>(T value, T minimum, T maximum)
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        if (minimum > maximum)
        {
            return Result.Failure<Bounded<T>>(
                new Error(
                    "bounded.invalid_range",
                    $"Minimum {minimum} is greater than maximum {maximum}."));
        }

        return Result.Success(
            new Bounded<T>(Bounded<T>.Clamp(value, minimum, maximum), minimum, maximum));
    }

    /// <summary>For scenario and test construction where the range is known to be valid.</summary>
    public static Bounded<T> From<T>(T value, T minimum, T maximum)
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        var result = Create(value, minimum, maximum);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Message);
    }

    /// <summary>A 0-100 attribute, the default range for most game values.</summary>
    public static Bounded<T> Percentage<T>(T value)
        where T : struct, INumber<T>, IMinMaxValue<T> =>
        From(value, T.Zero, T.CreateChecked(100));

    /// <summary>A -100..+100 ideological axis position.</summary>
    public static Bounded<T> AxisPosition<T>(T value)
        where T : struct, INumber<T>, IMinMaxValue<T> =>
        From(value, T.CreateChecked(-100), T.CreateChecked(100));

    /// <summary>A 0-1 proportion, used for intensity, turnout and probability.</summary>
    public static Bounded<T> UnitInterval<T>(T value)
        where T : struct, INumber<T>, IMinMaxValue<T> =>
        From(value, T.Zero, T.One);
}
