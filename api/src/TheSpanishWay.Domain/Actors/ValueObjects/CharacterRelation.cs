using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Actors.ValueObjects;

/// <summary>
/// How one politician regards another. Directed and frequently asymmetric: a deputy may
/// admire a leader who barely registers them.
/// </summary>
public sealed record CharacterRelation(
    CharacterId Source,
    CharacterId Target,
    decimal Affinity,
    decimal Trust,
    decimal Respect)
{
    public static CharacterRelation Neutral(CharacterId source, CharacterId target) =>
        new(source, target, 50m, 50m, 50m);
}
