namespace TheSpanishWay.Domain.Politics.Enumerations;

/// <summary>Whether a party is still in play.</summary>
public enum PartyStatus
{
    Active = 0,

    /// <summary>Still exists but no longer contests elections.</summary>
    Inactive = 1,

    /// <summary>Gone. Only reachable through the survival policy.</summary>
    Dissolved = 2,
}

/// <summary>What a member does in their party.</summary>
public enum PartyRole
{
    Member = 0,
    Spokesperson = 1,
    DeputyLeader = 2,
    Leader = 3,
}

/// <summary>Whether a membership is live, and how it ended if not.</summary>
public enum MembershipStatus
{
    Active = 0,
    Resigned = 1,
    Expelled = 2,
    Defected = 3,
}

/// <summary>Where a party sits relative to the government.</summary>
public enum GovernmentPosition
{
    Opposition = 0,

    /// <summary>Votes with the government without holding ministries.</summary>
    ParliamentarySupport = 1,

    Coalition = 2,
    Lead = 3,
}
