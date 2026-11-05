using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// A polling company, with the bias it brings to every poll it runs.
/// <para>
/// House effects live in state rather than being drawn fresh each month. A pollster that
/// consistently overstates one party is a recognisable feature of real polling, and one
/// whose bias wandered randomly would be indistinguishable from noise.
/// </para>
/// </summary>
public sealed record Pollster(
    PollsterId Id,
    string Name,
    IReadOnlyDictionary<PartyId, decimal> HouseEffects)
{
    /// <summary>This pollster's standing bias toward a party, in percentage points.</summary>
    public decimal HouseEffectFor(PartyId party) =>
        HouseEffects.TryGetValue(party, out var effect) ? effect : 0m;
}

/// <summary>
/// A published estimate. Not the hidden state, and not the result.
/// <para>
/// Three different things that v0.1 risks conflating: what voters actually intend, what a
/// poll reports, and what an election returns. Keeping them apart is what lets a result
/// surprise a player who has been reading the polls.
/// </para>
/// </summary>
public sealed record Poll(
    PollsterId Pollster,
    GameDate PublishedOn,
    int SampleSize,
    decimal MarginOfError,
    IReadOnlyDictionary<PartyId, decimal> EstimatedShares)
{
    public decimal ShareFor(PartyId party) =>
        EstimatedShares.TryGetValue(party, out var share) ? share : 0m;
}
