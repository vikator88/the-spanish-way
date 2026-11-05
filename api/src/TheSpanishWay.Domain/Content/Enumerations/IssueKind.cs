namespace TheSpanishWay.Domain.Content.Enumerations;

/// <summary>
/// Whether an issue is about <em>where you stand</em> or about <em>whether you are any good</em>.
/// <para>
/// v0.1 uses <c>IssueType</c> throughout without ever defining it, and weights issues
/// separately from the six axes without saying how the two relate. The distinction matters
/// because ideology decides a reaction to a stance on pensions, and has nothing to say about
/// a corruption scandal. See design D9.
/// </para>
/// </summary>
public enum IssueKind
{
    /// <summary>
    /// Has a stance. Loads onto one or more ideological axes — pensions onto the economy —
    /// so reactions depend on how far an actor's position sits from the segment's own.
    /// </summary>
    Positional = 0,

    /// <summary>
    /// Has no stance, only competence or blame: corruption, unemployment. Nobody is in favour
    /// of corruption, so reactions depend on attribution rather than on ideological distance.
    /// </summary>
    Valence = 1,
}
