using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Politics.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Politics.ValueObjects;

/// <summary>
/// A character's place in a party.
/// <para>
/// <see cref="InternalSupport"/> is deliberately not called loyalty: a character already has
/// a <c>Loyalty</c> trait, and the two mean entirely different things. One is a personality
/// trait; this is how much the party's own people back you. Review issue #8.
/// </para>
/// <para>
/// <see cref="Since"/> is a <see cref="GameDate"/>, not a wall-clock timestamp. v0.1 uses
/// <c>DateTimeOffset</c> here, which would tie a saved game to when it was played.
/// </para>
/// </summary>
public sealed record PartyMembership(
    CharacterId Character,
    PartyRole Role,
    GameDate Since,
    decimal InternalSupport,
    decimal Influence,
    MembershipStatus Status)
{
    public bool IsActive => Status == MembershipStatus.Active;

    public bool IsLeadership => Role is PartyRole.Leader or PartyRole.DeputyLeader or PartyRole.Spokesperson;

    public static Result<PartyMembership> Create(
        CharacterId character,
        PartyRole role,
        GameDate since,
        decimal internalSupport = 50m,
        decimal influence = 50m)
    {
        if (internalSupport is < 0m or > 100m)
        {
            return Result.Failure<PartyMembership>(
                new Error("membership.support_out_of_range", $"Internal support {internalSupport} is outside 0–100."));
        }

        if (influence is < 0m or > 100m)
        {
            return Result.Failure<PartyMembership>(
                new Error("membership.influence_out_of_range", $"Influence {influence} is outside 0–100."));
        }

        return Result.Success(
            new PartyMembership(character, role, since, internalSupport, influence, MembershipStatus.Active));
    }
}

/// <summary>
/// A directed relationship between two parties. Asymmetric by design: A may trust B far more
/// than B trusts A, and ideological proximity implies neither.
/// </summary>
public sealed record PartyRelation(
    PartyId Source,
    PartyId Target,
    decimal Affinity,
    decimal Trust,
    decimal Respect)
{
    public static PartyRelation Neutral(PartyId source, PartyId target) =>
        new(source, target, 50m, 50m, 50m);
}

/// <summary>
/// A party's organisation in one province.
/// <para>
/// Feeds electoral utility and mobilisation. It never awards a fixed vote quantity — that
/// would be the content placing votes by hand, which the model refuses everywhere else too.
/// </para>
/// </summary>
public sealed record PartyProvincePresence(
    ProvinceId Province,
    decimal OrganisationalStrength,
    decimal LocalLeadershipStrength,
    decimal TerritorialInfluence)
{
    public static PartyProvincePresence Absent(ProvinceId province) => new(province, 0m, 0m, 0m);
}
