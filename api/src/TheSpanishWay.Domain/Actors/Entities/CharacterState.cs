using TheSpanishWay.Domain.Actors.Enumerations;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Actors.Entities;

/// <summary>
/// A politician.
/// <para>
/// Holds no party id. Affiliation is derived from active memberships, so a character and a
/// party cannot disagree about whether they belong together.
/// </para>
/// <para>
/// <see cref="Popularity"/> is standing with the public and has nothing to do with standing
/// inside the party, which lives on the membership as <c>InternalSupport</c>. A character
/// can be loved by the country and despised by their own deputies; both are valid at once
/// and nothing reconciles them.
/// </para>
/// </summary>
public sealed class CharacterState : Entity<CharacterId>
{
    private CharacterState(
        CharacterId id,
        string name,
        IdeologicalProfile ideology,
        Bounded<decimal> popularity,
        Bounded<decimal> charisma,
        Bounded<decimal> loyalty,
        Bounded<decimal> ambition,
        Bounded<decimal> negotiationSkill)
        : base(id)
    {
        Name = name;
        Ideology = ideology;
        Popularity = popularity;
        Charisma = charisma;
        Loyalty = loyalty;
        Ambition = ambition;
        NegotiationSkill = negotiationSkill;
        Status = CharacterStatus.Active;
    }

    public string Name { get; }

    public IdeologicalProfile Ideology { get; }

    /// <summary>Standing with the public.</summary>
    public Bounded<decimal> Popularity { get; private set; }

    public Bounded<decimal> Charisma { get; private set; }

    /// <summary>A personality trait, not a measure of party backing.</summary>
    public Bounded<decimal> Loyalty { get; private set; }

    public Bounded<decimal> Ambition { get; private set; }

    public Bounded<decimal> NegotiationSkill { get; private set; }

    public CharacterStatus Status { get; private set; }

    public bool IsActive => Status == CharacterStatus.Active;

    public static Result<CharacterState> Create(
        CharacterId id,
        string name,
        IdeologicalProfile ideology,
        decimal popularity = 50m,
        decimal charisma = 50m,
        decimal loyalty = 50m,
        decimal ambition = 50m,
        decimal negotiationSkill = 50m)
    {
        ArgumentNullException.ThrowIfNull(ideology);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<CharacterState>(
                new Error("character.name_required", "A character needs a name."));
        }

        return Result.Success(
            new CharacterState(
                id, name, ideology,
                Bounded.Percentage(popularity), Bounded.Percentage(charisma),
                Bounded.Percentage(loyalty), Bounded.Percentage(ambition),
                Bounded.Percentage(negotiationSkill)));
    }

    public BoundedChange<decimal> AdjustPopularity(decimal delta)
    {
        var change = Popularity.Add(delta);
        Popularity = change.Result;

        return change;
    }

    public BoundedChange<decimal> AdjustLoyalty(decimal delta)
    {
        var change = Loyalty.Add(delta);
        Loyalty = change.Result;

        return change;
    }

    public void Retire() => Status = CharacterStatus.Retired;

    public void RecordDeath() => Status = CharacterStatus.Deceased;
}
