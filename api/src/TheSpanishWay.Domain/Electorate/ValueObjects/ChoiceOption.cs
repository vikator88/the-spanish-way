using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// Something a voting segment can choose: a party standing here, or a blank ballot.
/// <para>
/// Abstention is <em>not</em> among these. v0.1 puts abstention inside the softmax and also
/// computes turnout separately, which counts it twice. Here turnout decides whether a
/// segment votes and the softmax decides whom it votes for. "Undecided" is a polling
/// concept and never appears in the hidden state at all. See design D9.
/// </para>
/// </summary>
public sealed record ChoiceOption(PartyId? Party, decimal Utility)
{
    /// <summary>True for the blank ballot, which is a vote cast but not for anyone.</summary>
    public bool IsBlank => Party is null;

    public static ChoiceOption For(PartyId party, decimal utility) => new(party, utility);

    /// <summary>
    /// A blank ballot, whose utility is how appealing voting for nobody looks. Blank votes
    /// count toward valid votes and so toward the 3% threshold.
    /// </summary>
    public static ChoiceOption Blank(decimal utility) => new(null, utility);
}

/// <summary>What a segment in a province is likely to do, given that it votes at all.</summary>
public sealed record ChoiceProbabilities(IReadOnlyDictionary<PartyId, decimal> ByParty, decimal Blank)
{
    public decimal Total => ByParty.Values.Sum() + Blank;

    public decimal For(PartyId party) => ByParty.TryGetValue(party, out var p) ? p : 0m;
}
