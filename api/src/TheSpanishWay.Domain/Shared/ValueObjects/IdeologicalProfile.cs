using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Enumerations;

namespace TheSpanishWay.Domain.Shared.ValueObjects;

/// <summary>
/// A position on each of the six ideological axes, every one of them in −100 to +100.
/// <para>
/// Segments, parties and characters all use this one type. There is no separate
/// <c>PartyIdeology</c>: a party's declared position and a segment's own position have the
/// same shape and must be directly comparable, since the whole point is to measure the
/// distance between them. Two identical types would only create an opportunity for them to
/// drift apart.
/// </para>
/// </summary>
public sealed class IdeologicalProfile : ValueObject
{
    private const decimal Extreme = 100m;

    private static readonly IdeologicalAxis[] AllAxes = Enum.GetValues<IdeologicalAxis>();

    private readonly decimal[] _positions;

    private IdeologicalProfile(decimal[] positions) => _positions = positions;

    /// <summary>Dead centre on every axis.</summary>
    public static IdeologicalProfile Centre { get; } = new(new decimal[AllAxes.Length]);

    public decimal On(IdeologicalAxis axis) => _positions[(int)axis];

    public static Result<IdeologicalProfile> Create(IReadOnlyDictionary<IdeologicalAxis, decimal> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var values = new decimal[AllAxes.Length];

        foreach (var axis in AllAxes)
        {
            if (!positions.TryGetValue(axis, out var position))
            {
                return Result.Failure<IdeologicalProfile>(
                    new Error("ideology.missing_axis", $"No position given for the {axis} axis."));
            }

            if (position < -Extreme || position > Extreme)
            {
                return Result.Failure<IdeologicalProfile>(
                    new Error(
                        "ideology.position_out_of_range",
                        $"Position {position} on {axis} is outside −100 to +100."));
            }

            values[(int)axis] = position;
        }

        return Result.Success(new IdeologicalProfile(values));
    }

    /// <summary>Positions in axis order, for tests and scenario authoring.</summary>
    public static IdeologicalProfile From(
        decimal economy,
        decimal socialValues,
        decimal territory,
        decimal immigration,
        decimal institutions,
        decimal security)
    {
        var result = Create(new Dictionary<IdeologicalAxis, decimal>
        {
            [IdeologicalAxis.Economy] = economy,
            [IdeologicalAxis.SocialValues] = socialValues,
            [IdeologicalAxis.Territory] = territory,
            [IdeologicalAxis.Immigration] = immigration,
            [IdeologicalAxis.Institutions] = institutions,
            [IdeologicalAxis.Security] = security,
        });

        return result.IsSuccess ? result.Value : throw new ArgumentException(result.Error.Message);
    }

    /// <summary>Every axis set to the same position.</summary>
    public static IdeologicalProfile Uniform(decimal position) =>
        From(position, position, position, position, position, position);

    /// <summary>
    /// This profile shifted by per-axis offsets and clamped back into range. Used for a
    /// segment's position in a particular province, which is what makes regionalist politics
    /// possible at all.
    /// </summary>
    public IdeologicalProfile ShiftedBy(IReadOnlyDictionary<IdeologicalAxis, decimal> offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);

        var shifted = new decimal[AllAxes.Length];

        foreach (var axis in AllAxes)
        {
            var offset = offsets.TryGetValue(axis, out var value) ? value : 0m;

            shifted[(int)axis] = Math.Clamp(On(axis) + offset, -Extreme, Extreme);
        }

        return new IdeologicalProfile(shifted);
    }

    /// <summary>
    /// This profile seen as more extreme or more moderate than it is.
    /// <para>
    /// A drift of +0.2 reads every position as 20% further from the centre; −0.2 reads it as
    /// 20% nearer. This is how a segment can misjudge a party without the party moving.
    /// </para>
    /// </summary>
    public IdeologicalProfile AsPerceived(decimal drift)
    {
        var scale = 1m + drift;
        var perceived = new decimal[AllAxes.Length];

        foreach (var axis in AllAxes)
        {
            perceived[(int)axis] = Math.Clamp(On(axis) * scale, -Extreme, Extreme);
        }

        return new IdeologicalProfile(perceived);
    }

    protected override IEnumerable<object?> GetEqualityComponents() => _positions.Cast<object?>();

    public override string ToString() =>
        string.Join(", ", AllAxes.Select(a => $"{a}={On(a)}"));
}
