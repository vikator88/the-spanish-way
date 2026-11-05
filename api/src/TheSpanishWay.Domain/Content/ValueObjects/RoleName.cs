using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.Content.ValueObjects;

/// <summary>
/// A slot in a reusable definition, written <c>$accused</c> or <c>$figure</c>.
/// <para>
/// This is what lets thirty event definitions be reusable at all. A "corruption scandal"
/// needs to say <em>which</em> party and <em>which</em> character, and it cannot name them
/// literally without becoming a one-off. Conditions, effects, templates and stimuli all
/// refer to roles; the concrete ids are bound when the event fires. See design D6.
/// </para>
/// </summary>
public readonly record struct RoleName : IComparable<RoleName>
{
    private RoleName(string value) => Value = value;

    public string Value { get; }

    /// <summary>The player's own party, available without declaration.</summary>
    public static RoleName Player { get; } = new("$player");

    /// <summary>The party leading the current government.</summary>
    public static RoleName GovernmentLead { get; } = new("$government.lead");

    /// <summary>Whatever the definition is attached to.</summary>
    public static RoleName Self { get; } = new("$self");

    /// <summary>Roles every definition may use without declaring them.</summary>
    public static IReadOnlySet<RoleName> BuiltIn { get; } =
        new HashSet<RoleName> { Player, GovernmentLead, Self };

    public bool IsBuiltIn => BuiltIn.Contains(this);

    public static Result<RoleName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<RoleName>(new Error("role.empty_name", "A role needs a name."));
        }

        var trimmed = value.Trim().ToLowerInvariant();

        if (!trimmed.StartsWith('$'))
        {
            return Result.Failure<RoleName>(
                new Error("role.missing_sigil", $"Role '{value}' must start with '$'."));
        }

        return Result.Success(new RoleName(trimmed));
    }

    public static RoleName From(string value)
    {
        var result = Create(value);

        return result.IsSuccess ? result.Value : throw new ArgumentException(result.Error.Message, nameof(value));
    }

    /// <summary>True when a reference looks like a role rather than a literal id.</summary>
    public static bool IsRoleToken(string instance) =>
        !string.IsNullOrWhiteSpace(instance) && instance.TrimStart().StartsWith('$');

    public int CompareTo(RoleName other) => string.CompareOrdinal(Value, other.Value);

    public static bool operator <(RoleName left, RoleName right) => left.CompareTo(right) < 0;

    public static bool operator <=(RoleName left, RoleName right) => left.CompareTo(right) <= 0;

    public static bool operator >(RoleName left, RoleName right) => left.CompareTo(right) > 0;

    public static bool operator >=(RoleName left, RoleName right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value;
}
