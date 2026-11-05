using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Politics.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Politics.Entities;

/// <summary>
/// Who is governing, and on whose votes.
/// <para>
/// Coalition membership and external parliamentary support are kept apart: a party that
/// votes with the government but holds no ministries is in a different position from one
/// sitting round the cabinet table, and the two attract different amounts of blame.
/// </para>
/// </summary>
public sealed class GovernmentState
{
    private readonly HashSet<PartyId> _coalition;
    private readonly HashSet<PartyId> _support;

    private GovernmentState(
        PartyId lead,
        IEnumerable<PartyId> coalition,
        IEnumerable<PartyId> support,
        GameDate startDate,
        Bounded<decimal> stability)
    {
        LeadParty = lead;
        _coalition = [.. coalition];
        _support = [.. support];
        StartDate = startDate;
        Stability = stability;
    }

    public PartyId LeadParty { get; }

    /// <summary>Parties in government, holding ministries. Includes the lead party.</summary>
    public IReadOnlyList<PartyId> Coalition => [.. _coalition.OrderBy(p => p)];

    /// <summary>Parties that vote with the government without being in it.</summary>
    public IReadOnlyList<PartyId> ParliamentarySupport => [.. _support.OrderBy(p => p)];

    public GameDate StartDate { get; }

    public GameDate? EndDate { get; private set; }

    public Bounded<decimal> Stability { get; private set; }

    public bool IsInOffice => EndDate is null;

    public static Result<GovernmentState> Form(
        PartyId lead,
        IReadOnlyCollection<PartyId> coalition,
        IReadOnlyCollection<PartyId> support,
        GameDate startDate,
        IReadOnlyCollection<PartyId> partiesInGame,
        decimal stability = 60m)
    {
        ArgumentNullException.ThrowIfNull(coalition);
        ArgumentNullException.ThrowIfNull(support);
        ArgumentNullException.ThrowIfNull(partiesInGame);

        var members = coalition.Append(lead).Concat(support).Distinct().ToList();

        foreach (var party in members.Where(p => !partiesInGame.Contains(p)))
        {
            return Result.Failure<GovernmentState>(
                new Error("government.unknown_party", $"Party {party} is not in this game."));
        }

        var overlapping = coalition.Intersect(support).ToList();

        if (overlapping.Count > 0)
        {
            return Result.Failure<GovernmentState>(
                new Error(
                    "government.coalition_and_support_overlap",
                    "A party cannot both sit in government and merely support it."));
        }

        return Result.Success(
            new GovernmentState(
                lead, coalition.Append(lead).Distinct(), support, startDate,
                Bounded.Percentage(stability)));
    }

    /// <summary>Where a party sits relative to this government.</summary>
    public GovernmentPosition PositionOf(PartyId party)
    {
        if (party == LeadParty)
        {
            return GovernmentPosition.Lead;
        }

        if (_coalition.Contains(party))
        {
            return GovernmentPosition.Coalition;
        }

        return _support.Contains(party)
            ? GovernmentPosition.ParliamentarySupport
            : GovernmentPosition.Opposition;
    }

    public void End(GameDate on) => EndDate = on;

    public BoundedChange<decimal> AdjustStability(decimal delta)
    {
        var change = Stability.Add(delta);
        Stability = change.Result;

        return change;
    }
}
