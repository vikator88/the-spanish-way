namespace TheSpanishWay.Domain.Actions.Enumerations;

/// <summary>
/// The three kinds of thing a party can do in the MVP.
/// <para>
/// One handler per category, with the specifics coming from data. v0.1's suggested project
/// tree lists <c>PublicPositionAction.cs</c> and friends as classes, which contradicts its
/// own instruction not to create a class per action. A category is a behaviour; an action is
/// content.
/// </para>
/// </summary>
public enum ActionCategory
{
    /// <summary>Statements, support, condemnation, putting a topic on the agenda.</summary>
    PublicPosition = 0,

    /// <summary>Votes on bills, budgets, motions and investitures.</summary>
    ParliamentaryVote = 1,

    /// <summary>Hiring, firing, promotion, expulsion, leadership changes.</summary>
    InternalParty = 2,
}

/// <summary>How an attempted action ended.</summary>
public enum ActionExecutionStatus
{
    Executed = 0,
    Rejected = 1,
}

/// <summary>Which way a party voted on a bill.</summary>
public enum VoteChoice
{
    Against = 0,
    Abstain = 1,
    For = 2,
}
