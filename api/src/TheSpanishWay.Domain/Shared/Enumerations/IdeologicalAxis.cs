namespace TheSpanishWay.Domain.Shared.Enumerations;

/// <summary>
/// The six axes every political position is expressed on, each running from −100 to +100.
/// <para>
/// Segments, parties and characters all use these, which is what makes ideological distance
/// between any two of them computable. The labels describe the poles; what matters is that
/// the numeric direction stays consistent everywhere.
/// </para>
/// </summary>
public enum IdeologicalAxis
{
    /// <summary>−100 interventionism, +100 liberalism.</summary>
    Economy = 0,

    /// <summary>−100 progressivism, +100 conservatism.</summary>
    SocialValues = 1,

    /// <summary>−100 centralism, +100 decentralisation.</summary>
    Territory = 2,

    /// <summary>−100 openness, +100 restriction.</summary>
    Immigration = 3,

    /// <summary>−100 regeneration, +100 continuity.</summary>
    Institutions = 4,

    /// <summary>−100 civil liberties, +100 authority.</summary>
    Security = 5,
}
