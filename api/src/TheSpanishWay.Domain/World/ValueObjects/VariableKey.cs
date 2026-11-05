using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.World.ValueObjects;

/// <summary>
/// Names a world indicator, for example <c>inflation</c> or <c>education</c>.
/// <para>
/// Indicators are data, not classes: there is no <c>Inflation</c> type and no
/// <c>Unemployment</c> type, because a scenario must be able to add an indicator without a
/// code change. Keys are normalised so content and code cannot disagree over casing.
/// </para>
/// </summary>
public readonly record struct VariableKey : IComparable<VariableKey>
{
    private VariableKey(string value) => Value = value;

    public string Value { get; }

    public static Result<VariableKey> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<VariableKey>(
                new Error("variable_key.empty", "A variable key cannot be blank."));
        }

        return Result.Success(new VariableKey(value.Trim().ToLowerInvariant()));
    }

    public static VariableKey From(string value)
    {
        var result = Create(value);

        return result.IsSuccess ? result.Value : throw new ArgumentException(result.Error.Message, nameof(value));
    }

    public int CompareTo(VariableKey other) =>
        string.CompareOrdinal(Value, other.Value);

    public static bool operator <(VariableKey left, VariableKey right) => left.CompareTo(right) < 0;

    public static bool operator <=(VariableKey left, VariableKey right) => left.CompareTo(right) <= 0;

    public static bool operator >(VariableKey left, VariableKey right) => left.CompareTo(right) > 0;

    public static bool operator >=(VariableKey left, VariableKey right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value;
}
