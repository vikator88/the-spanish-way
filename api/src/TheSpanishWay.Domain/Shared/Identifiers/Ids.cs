namespace TheSpanishWay.Domain.Shared.Identifiers;

// Strongly-typed identifiers.
//
// Each is a distinct type, so passing a PartyId where a CharacterId is expected fails to
// compile rather than corrupting a lookup at runtime. All are comparable, because the
// simulation iterates every collection in identifier order: dictionary and hash-set
// enumeration order is not a contract, and determinism depends on that. See
// domain/building-blocks and domain/game-state, "Stable iteration order".
//
// Deliberately no New(): identifiers minted during a tick must come from a deterministic
// source, not a clock- or entropy-backed Guid, or replay breaks. Scenario loading and the
// Application layer supply ids explicitly through From().

/// <summary>Identifies a running game.</summary>
public readonly record struct GameId(Guid Value) : IComparable<GameId>
{
    public static readonly GameId Empty = new(Guid.Empty);

    public static GameId From(Guid value) => new(value);

    public int CompareTo(GameId other) => Value.CompareTo(other.Value);

    public static bool operator <(GameId left, GameId right) => left.CompareTo(right) < 0;

    public static bool operator <=(GameId left, GameId right) => left.CompareTo(right) <= 0;

    public static bool operator >(GameId left, GameId right) => left.CompareTo(right) > 0;

    public static bool operator >=(GameId left, GameId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a political party.</summary>
public readonly record struct PartyId(Guid Value) : IComparable<PartyId>
{
    public static readonly PartyId Empty = new(Guid.Empty);

    public static PartyId From(Guid value) => new(value);

    public int CompareTo(PartyId other) => Value.CompareTo(other.Value);

    public static bool operator <(PartyId left, PartyId right) => left.CompareTo(right) < 0;

    public static bool operator <=(PartyId left, PartyId right) => left.CompareTo(right) <= 0;

    public static bool operator >(PartyId left, PartyId right) => left.CompareTo(right) > 0;

    public static bool operator >=(PartyId left, PartyId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a political character.</summary>
public readonly record struct CharacterId(Guid Value) : IComparable<CharacterId>
{
    public static readonly CharacterId Empty = new(Guid.Empty);

    public static CharacterId From(Guid value) => new(value);

    public int CompareTo(CharacterId other) => Value.CompareTo(other.Value);

    public static bool operator <(CharacterId left, CharacterId right) => left.CompareTo(right) < 0;

    public static bool operator <=(CharacterId left, CharacterId right) => left.CompareTo(right) <= 0;

    public static bool operator >(CharacterId left, CharacterId right) => left.CompareTo(right) > 0;

    public static bool operator >=(CharacterId left, CharacterId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an electoral constituency.</summary>
public readonly record struct ProvinceId(Guid Value) : IComparable<ProvinceId>
{
    public static readonly ProvinceId Empty = new(Guid.Empty);

    public static ProvinceId From(Guid value) => new(value);

    public int CompareTo(ProvinceId other) => Value.CompareTo(other.Value);

    public static bool operator <(ProvinceId left, ProvinceId right) => left.CompareTo(right) < 0;

    public static bool operator <=(ProvinceId left, ProvinceId right) => left.CompareTo(right) <= 0;

    public static bool operator >(ProvinceId left, ProvinceId right) => left.CompareTo(right) > 0;

    public static bool operator >=(ProvinceId left, ProvinceId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a population segment.</summary>
public readonly record struct SegmentId(Guid Value) : IComparable<SegmentId>
{
    public static readonly SegmentId Empty = new(Guid.Empty);

    public static SegmentId From(Guid value) => new(value);

    public int CompareTo(SegmentId other) => Value.CompareTo(other.Value);

    public static bool operator <(SegmentId left, SegmentId right) => left.CompareTo(right) < 0;

    public static bool operator <=(SegmentId left, SegmentId right) => left.CompareTo(right) <= 0;

    public static bool operator >(SegmentId left, SegmentId right) => left.CompareTo(right) > 0;

    public static bool operator >=(SegmentId left, SegmentId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a media outlet.</summary>
public readonly record struct MediaOutletId(Guid Value) : IComparable<MediaOutletId>
{
    public static readonly MediaOutletId Empty = new(Guid.Empty);

    public static MediaOutletId From(Guid value) => new(value);

    public int CompareTo(MediaOutletId other) => Value.CompareTo(other.Value);

    public static bool operator <(MediaOutletId left, MediaOutletId right) => left.CompareTo(right) < 0;

    public static bool operator <=(MediaOutletId left, MediaOutletId right) => left.CompareTo(right) <= 0;

    public static bool operator >(MediaOutletId left, MediaOutletId right) => left.CompareTo(right) > 0;

    public static bool operator >=(MediaOutletId left, MediaOutletId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a polling company.</summary>
public readonly record struct PollsterId(Guid Value) : IComparable<PollsterId>
{
    public static readonly PollsterId Empty = new(Guid.Empty);

    public static PollsterId From(Guid value) => new(value);

    public int CompareTo(PollsterId other) => Value.CompareTo(other.Value);

    public static bool operator <(PollsterId left, PollsterId right) => left.CompareTo(right) < 0;

    public static bool operator <=(PollsterId left, PollsterId right) => left.CompareTo(right) <= 0;

    public static bool operator >(PollsterId left, PollsterId right) => left.CompareTo(right) > 0;

    public static bool operator >=(PollsterId left, PollsterId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an issue in the scenario's issue catalogue.</summary>
public readonly record struct IssueId(Guid Value) : IComparable<IssueId>
{
    public static readonly IssueId Empty = new(Guid.Empty);

    public static IssueId From(Guid value) => new(value);

    public int CompareTo(IssueId other) => Value.CompareTo(other.Value);

    public static bool operator <(IssueId left, IssueId right) => left.CompareTo(right) < 0;

    public static bool operator <=(IssueId left, IssueId right) => left.CompareTo(right) <= 0;

    public static bool operator >(IssueId left, IssueId right) => left.CompareTo(right) > 0;

    public static bool operator >=(IssueId left, IssueId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an authored simulation-event definition.</summary>
public readonly record struct EventDefinitionId(Guid Value) : IComparable<EventDefinitionId>
{
    public static readonly EventDefinitionId Empty = new(Guid.Empty);

    public static EventDefinitionId From(Guid value) => new(value);

    public int CompareTo(EventDefinitionId other) => Value.CompareTo(other.Value);

    public static bool operator <(EventDefinitionId left, EventDefinitionId right) => left.CompareTo(right) < 0;

    public static bool operator <=(EventDefinitionId left, EventDefinitionId right) => left.CompareTo(right) <= 0;

    public static bool operator >(EventDefinitionId left, EventDefinitionId right) => left.CompareTo(right) > 0;

    public static bool operator >=(EventDefinitionId left, EventDefinitionId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies one firing of an event definition within a game.</summary>
public readonly record struct EventOccurrenceId(Guid Value) : IComparable<EventOccurrenceId>
{
    public static readonly EventOccurrenceId Empty = new(Guid.Empty);

    public static EventOccurrenceId From(Guid value) => new(value);

    public int CompareTo(EventOccurrenceId other) => Value.CompareTo(other.Value);

    public static bool operator <(EventOccurrenceId left, EventOccurrenceId right) => left.CompareTo(right) < 0;

    public static bool operator <=(EventOccurrenceId left, EventOccurrenceId right) => left.CompareTo(right) <= 0;

    public static bool operator >(EventOccurrenceId left, EventOccurrenceId right) => left.CompareTo(right) > 0;

    public static bool operator >=(EventOccurrenceId left, EventOccurrenceId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an authored action definition.</summary>
public readonly record struct ActionDefinitionId(Guid Value) : IComparable<ActionDefinitionId>
{
    public static readonly ActionDefinitionId Empty = new(Guid.Empty);

    public static ActionDefinitionId From(Guid value) => new(value);

    public int CompareTo(ActionDefinitionId other) => Value.CompareTo(other.Value);

    public static bool operator <(ActionDefinitionId left, ActionDefinitionId right) => left.CompareTo(right) < 0;

    public static bool operator <=(ActionDefinitionId left, ActionDefinitionId right) => left.CompareTo(right) <= 0;

    public static bool operator >(ActionDefinitionId left, ActionDefinitionId right) => left.CompareTo(right) > 0;

    public static bool operator >=(ActionDefinitionId left, ActionDefinitionId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies one execution of an action within a game.</summary>
public readonly record struct ActionExecutionId(Guid Value) : IComparable<ActionExecutionId>
{
    public static readonly ActionExecutionId Empty = new(Guid.Empty);

    public static ActionExecutionId From(Guid value) => new(value);

    public int CompareTo(ActionExecutionId other) => Value.CompareTo(other.Value);

    public static bool operator <(ActionExecutionId left, ActionExecutionId right) => left.CompareTo(right) < 0;

    public static bool operator <=(ActionExecutionId left, ActionExecutionId right) => left.CompareTo(right) <= 0;

    public static bool operator >(ActionExecutionId left, ActionExecutionId right) => left.CompareTo(right) > 0;

    public static bool operator >=(ActionExecutionId left, ActionExecutionId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an authored effect declaration.</summary>
public readonly record struct EffectDefinitionId(Guid Value) : IComparable<EffectDefinitionId>
{
    public static readonly EffectDefinitionId Empty = new(Guid.Empty);

    public static EffectDefinitionId From(Guid value) => new(value);

    public int CompareTo(EffectDefinitionId other) => Value.CompareTo(other.Value);

    public static bool operator <(EffectDefinitionId left, EffectDefinitionId right) => left.CompareTo(right) < 0;

    public static bool operator <=(EffectDefinitionId left, EffectDefinitionId right) => left.CompareTo(right) <= 0;

    public static bool operator >(EffectDefinitionId left, EffectDefinitionId right) => left.CompareTo(right) > 0;

    public static bool operator >=(EffectDefinitionId left, EffectDefinitionId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a queued effect awaiting its apply date.</summary>
public readonly record struct PendingEffectId(Guid Value) : IComparable<PendingEffectId>
{
    public static readonly PendingEffectId Empty = new(Guid.Empty);

    public static PendingEffectId From(Guid value) => new(value);

    public int CompareTo(PendingEffectId other) => Value.CompareTo(other.Value);

    public static bool operator <(PendingEffectId left, PendingEffectId right) => left.CompareTo(right) < 0;

    public static bool operator <=(PendingEffectId left, PendingEffectId right) => left.CompareTo(right) <= 0;

    public static bool operator >(PendingEffectId left, PendingEffectId right) => left.CompareTo(right) > 0;

    public static bool operator >=(PendingEffectId left, PendingEffectId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies an active temporary modifier on an attribute.</summary>
public readonly record struct ModifierId(Guid Value) : IComparable<ModifierId>
{
    public static readonly ModifierId Empty = new(Guid.Empty);

    public static ModifierId From(Guid value) => new(value);

    public int CompareTo(ModifierId other) => Value.CompareTo(other.Value);

    public static bool operator <(ModifierId left, ModifierId right) => left.CompareTo(right) < 0;

    public static bool operator <=(ModifierId left, ModifierId right) => left.CompareTo(right) <= 0;

    public static bool operator >(ModifierId left, ModifierId right) => left.CompareTo(right) > 0;

    public static bool operator >=(ModifierId left, ModifierId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a scenario, independent of its published versions.</summary>
public readonly record struct ScenarioId(Guid Value) : IComparable<ScenarioId>
{
    public static readonly ScenarioId Empty = new(Guid.Empty);

    public static ScenarioId From(Guid value) => new(value);

    public int CompareTo(ScenarioId other) => Value.CompareTo(other.Value);

    public static bool operator <(ScenarioId left, ScenarioId right) => left.CompareTo(right) < 0;

    public static bool operator <=(ScenarioId left, ScenarioId right) => left.CompareTo(right) <= 0;

    public static bool operator >(ScenarioId left, ScenarioId right) => left.CompareTo(right) > 0;

    public static bool operator >=(ScenarioId left, ScenarioId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a held election.</summary>
public readonly record struct ElectionId(Guid Value) : IComparable<ElectionId>
{
    public static readonly ElectionId Empty = new(Guid.Empty);

    public static ElectionId From(Guid value) => new(value);

    public int CompareTo(ElectionId other) => Value.CompareTo(other.Value);

    public static bool operator <(ElectionId left, ElectionId right) => left.CompareTo(right) < 0;

    public static bool operator <=(ElectionId left, ElectionId right) => left.CompareTo(right) <= 0;

    public static bool operator >(ElectionId left, ElectionId right) => left.CompareTo(right) > 0;

    public static bool operator >=(ElectionId left, ElectionId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>Identifies the player who owns a game. Authorisation lives in the Application layer.</summary>
public readonly record struct OwnerId(Guid Value) : IComparable<OwnerId>
{
    public static readonly OwnerId Empty = new(Guid.Empty);

    public static OwnerId From(Guid value) => new(value);

    public int CompareTo(OwnerId other) => Value.CompareTo(other.Value);

    public static bool operator <(OwnerId left, OwnerId right) => left.CompareTo(right) < 0;

    public static bool operator <=(OwnerId left, OwnerId right) => left.CompareTo(right) <= 0;

    public static bool operator >(OwnerId left, OwnerId right) => left.CompareTo(right) > 0;

    public static bool operator >=(OwnerId left, OwnerId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}
