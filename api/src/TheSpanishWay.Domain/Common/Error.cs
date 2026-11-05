namespace TheSpanishWay.Domain.Common;

/// <summary>
/// A rule violation, carrying a stable machine-readable code so callers can branch on
/// the reason without parsing prose. Codes are dotted and namespaced by area, e.g.
/// <c>game.already_finished</c>.
/// </summary>
public sealed record Error(string Code, string Message)
{
    /// <summary>The absence of an error. Used by successful results.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    public override string ToString() => $"{Code}: {Message}";
}
