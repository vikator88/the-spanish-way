using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Politics.Enumerations;
using TheSpanishWay.Domain.Politics.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Politics.Entities;

/// <summary>
/// A political party as it stands right now.
/// <para>
/// A state component inside <c>GameState</c>, not an aggregate of its own. A daily tick
/// touches parties, characters and perceptions together under one revision, so giving this
/// its own repository would force a cross-aggregate transaction on every tick. See design D2.
/// </para>
/// <para>
/// It holds no seat count: seats are an election outcome and live in <c>Parliament</c>. It
/// holds no leader id either — the leader is whichever membership has that role, so the two
/// cannot disagree.
/// </para>
/// </summary>
public sealed class PartyState : Entity<PartyId>
{
    private readonly List<PartyMembership> _memberships;
    private readonly Dictionary<ProvinceId, PartyProvincePresence> _presence;

    private PartyState(
        PartyId id,
        string name,
        IdeologicalProfile ideology,
        Bounded<decimal> cohesion,
        Bounded<decimal> credibility,
        decimal budget)
        : base(id)
    {
        Name = name;
        DeclaredIdeology = ideology;
        Cohesion = cohesion;
        Credibility = credibility;
        Budget = budget;
        Status = PartyStatus.Active;
        _memberships = [];
        _presence = [];
    }

    public string Name { get; }

    /// <summary>What the party says it believes. Segments may perceive it differently.</summary>
    public IdeologicalProfile DeclaredIdeology { get; }

    public Bounded<decimal> Cohesion { get; private set; }

    public Bounded<decimal> Credibility { get; private set; }

    public decimal Budget { get; private set; }

    public PartyStatus Status { get; private set; }

    public bool IsActive => Status == PartyStatus.Active;

    /// <summary>How many consecutive months the budget has been in deficit, for the survival policy.</summary>
    public int ConsecutiveMonthsInDeficit { get; private set; }

    /// <summary>How many consecutive general elections returned no seat.</summary>
    public int ConsecutiveElectionsWithoutSeats { get; private set; }

    public IReadOnlyList<PartyMembership> Memberships =>
        [.. _memberships.OrderBy(m => m.Character)];

    public IReadOnlyList<PartyMembership> ActiveMemberships =>
        [.. _memberships.Where(m => m.IsActive).OrderBy(m => m.Character)];

    /// <summary>Derived, never stored: the member holding the Leader role.</summary>
    public CharacterId? LeaderId =>
        _memberships.FirstOrDefault(m => m.IsActive && m.Role == PartyRole.Leader)?.Character;

    public IReadOnlyList<PartyProvincePresence> Presence =>
        [.. _presence.Values.OrderBy(p => p.Province)];

    public static Result<PartyState> Create(
        PartyId id,
        string name,
        IdeologicalProfile ideology,
        decimal cohesion = 60m,
        decimal credibility = 55m,
        decimal budget = 1_000_000m)
    {
        ArgumentNullException.ThrowIfNull(ideology);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<PartyState>(new Error("party.name_required", "A party needs a name."));
        }

        return Result.Success(
            new PartyState(
                id, name, ideology,
                Bounded.Percentage(cohesion), Bounded.Percentage(credibility), budget));
    }

    public BoundedChange<decimal> AdjustCohesion(decimal delta)
    {
        var change = Cohesion.Add(delta);
        Cohesion = change.Result;

        return change;
    }

    public BoundedChange<decimal> AdjustCredibility(decimal delta)
    {
        var change = Credibility.Add(delta);
        Credibility = change.Result;

        return change;
    }

    /// <summary>Spends from the budget. A party may go into deficit; that is the survival policy's problem.</summary>
    public Result Spend(decimal amount)
    {
        if (amount < 0m)
        {
            return Result.Failure(new Error("party.negative_spend", "Cannot spend a negative amount."));
        }

        Budget -= amount;

        return Result.Success();
    }

    public void Receive(decimal amount) => Budget += amount;

    public Result AddMember(PartyMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        if (_memberships.Any(m => m.Character == membership.Character && m.IsActive))
        {
            return Result.Failure(
                new Error(
                    "party.duplicate_membership",
                    $"Character {membership.Character} already holds an active membership."));
        }

        if (membership.Role == PartyRole.Leader && LeaderId is not null)
        {
            return Result.Failure(
                new Error("party.leader_already_exists", $"{Name} already has a leader."));
        }

        _memberships.Add(membership);

        return Result.Success();
    }

    /// <summary>
    /// Hands leadership to an existing, active member. A party cannot be led by someone who
    /// is not in it.
    /// </summary>
    public Result<CharacterId?> ChangeLeader(CharacterId newLeader)
    {
        var incoming = _memberships.FirstOrDefault(m => m.Character == newLeader && m.IsActive);

        if (incoming is null)
        {
            return Result.Failure<CharacterId?>(
                new Error(
                    "party.leader_not_a_member",
                    $"Character {newLeader} is not an active member of {Name}."));
        }

        var previous = LeaderId;

        if (previous == newLeader)
        {
            return Result.Success<CharacterId?>(previous);
        }

        Replace(m => m.Role == PartyRole.Leader && m.IsActive, m => m with { Role = PartyRole.Member });
        Replace(m => m.Character == newLeader, m => m with { Role = PartyRole.Leader });

        return Result.Success(previous);
    }

    public Result Expel(CharacterId character) =>
        EndMembership(character, MembershipStatus.Expelled);

    public Result AcceptResignation(CharacterId character) =>
        EndMembership(character, MembershipStatus.Resigned);

    public Result RecordDefection(CharacterId character) =>
        EndMembership(character, MembershipStatus.Defected);

    public Result Promote(CharacterId character, PartyRole role)
    {
        if (role == PartyRole.Leader)
        {
            var changed = ChangeLeader(character);

            return changed.IsFailure ? Result.Failure(changed.Error) : Result.Success();
        }

        if (!_memberships.Any(m => m.Character == character && m.IsActive))
        {
            return Result.Failure(
                new Error("party.not_a_member", $"Character {character} is not an active member."));
        }

        Replace(m => m.Character == character && m.IsActive, m => m with { Role = role });

        return Result.Success();
    }

    public void SetPresence(PartyProvincePresence presence)
    {
        ArgumentNullException.ThrowIfNull(presence);

        _presence[presence.Province] = presence;
    }

    public PartyProvincePresence PresenceIn(ProvinceId province) =>
        _presence.TryGetValue(province, out var presence)
            ? presence
            : PartyProvincePresence.Absent(province);

    /// <summary>Recorded monthly, so the survival policy can see a run of deficits.</summary>
    public void RecordMonthEnd() =>
        ConsecutiveMonthsInDeficit = Budget < 0m ? ConsecutiveMonthsInDeficit + 1 : 0;

    public void RecordElectionResult(int seatsWon) =>
        ConsecutiveElectionsWithoutSeats = seatsWon > 0 ? 0 : ConsecutiveElectionsWithoutSeats + 1;

    /// <summary>
    /// Only the survival policy calls this. Losing an election does not dissolve a party, and
    /// nothing else may decide that it should.
    /// </summary>
    internal void Dissolve() => Status = PartyStatus.Dissolved;

    internal void Deactivate() => Status = PartyStatus.Inactive;

    private Result EndMembership(CharacterId character, MembershipStatus status)
    {
        var membership = _memberships.FirstOrDefault(m => m.Character == character && m.IsActive);

        if (membership is null)
        {
            return Result.Failure(
                new Error("party.not_a_member", $"Character {character} is not an active member of {Name}."));
        }

        Replace(m => m.Character == character && m.IsActive, m => m with { Status = status });

        return Result.Success();
    }

    private void Replace(Func<PartyMembership, bool> match, Func<PartyMembership, PartyMembership> change)
    {
        for (var index = 0; index < _memberships.Count; index++)
        {
            if (match(_memberships[index]))
            {
                _memberships[index] = change(_memberships[index]);
            }
        }
    }
}
