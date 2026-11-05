using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.Electorate.ValueObjects;

/// <summary>
/// Where each party actually stands.
/// <para>
/// A party does not necessarily contest every constituency, and a regional party contests
/// only a handful. Without this, every party would be on every ballot and regionalist
/// politics — which decides governments under this system — could not exist.
/// </para>
/// </summary>
public sealed class CandidacyRegister
{
    private readonly Dictionary<ProvinceId, HashSet<PartyId>> _standing = [];

    public static CandidacyRegister Empty() => new();

    /// <summary>Every party stands everywhere: the simple case, and a useful test default.</summary>
    public static CandidacyRegister Nationwide(
        IReadOnlyList<ProvinceId> provinces,
        IReadOnlyList<PartyId> parties)
    {
        ArgumentNullException.ThrowIfNull(provinces);
        ArgumentNullException.ThrowIfNull(parties);

        var register = new CandidacyRegister();

        foreach (var province in provinces)
        {
            foreach (var party in parties)
            {
                register.Stand(party, province);
            }
        }

        return register;
    }

    public CandidacyRegister Stand(PartyId party, ProvinceId province)
    {
        if (!_standing.TryGetValue(province, out var parties))
        {
            parties = [];
            _standing[province] = parties;
        }

        parties.Add(party);

        return this;
    }

    public bool IsStanding(PartyId party, ProvinceId province) =>
        _standing.TryGetValue(province, out var parties) && parties.Contains(party);

    /// <summary>The parties on the ballot here, in identifier order.</summary>
    public IReadOnlyList<PartyId> StandingIn(ProvinceId province) =>
        _standing.TryGetValue(province, out var parties) ? [.. parties.OrderBy(p => p)] : [];
}
