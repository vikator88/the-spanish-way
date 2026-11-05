using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.World.Entities;

namespace TheSpanishWay.Domain.Content.Aggregates;

/// <summary>
/// A world a game can be started from: its parties, its issues, its events, its opening
/// indicators and its electoral law.
/// <para>
/// The second aggregate, and the only immutable one. A scenario version becomes frozen when
/// it is published, and games pin a <em>version</em> rather than a scenario id — so editing
/// content to fix a typo cannot reach into a legislature already in progress and change its
/// rules halfway through. See design D2 and review issue #15.
/// </para>
/// </summary>
public sealed class ScenarioDefinition : AggregateRoot<ScenarioId>
{
    private readonly List<GameEventDefinition> _events;
    private readonly List<IssueDefinition> _issues;
    private readonly List<PartyId> _parties;
    private readonly List<WorldVariable> _initialVariables;

    private ScenarioDefinition(
        ScenarioId id,
        string name,
        int version,
        IEnumerable<GameEventDefinition> events,
        IEnumerable<IssueDefinition> issues,
        IEnumerable<PartyId> parties,
        IEnumerable<WorldVariable> initialVariables)
        : base(id)
    {
        Name = name;
        Version = version;
        _events = [.. events];
        _issues = [.. issues];
        _parties = [.. parties];
        _initialVariables = [.. initialVariables];
    }

    public string Name { get; }

    public int Version { get; }

    /// <summary>Frozen. A published version never changes again; editing produces a new one.</summary>
    public bool IsPublished { get; private set; }

    public IReadOnlyList<GameEventDefinition> Events =>
        [.. _events.OrderBy(e => e.Key, StringComparer.Ordinal)];

    public IReadOnlyList<IssueDefinition> Issues =>
        [.. _issues.OrderBy(i => i.Key, StringComparer.Ordinal)];

    public IReadOnlyList<PartyId> Parties => [.. _parties.OrderBy(p => p)];

    public IReadOnlyList<WorldVariable> InitialVariables =>
        [.. _initialVariables.OrderBy(v => v.Key)];

    /// <summary>How a game refers to this exact version.</summary>
    public Games.ValueObjects.ScenarioVersionRef Reference => new(Id, Version);

    public static ScenarioDefinition Draft(ScenarioId id, string name) =>
        new(id, name, version: 1, [], [], [], []);

    public Result AddEvent(GameEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var frozen = EnsureEditable();

        if (frozen.IsFailure)
        {
            return frozen;
        }

        if (_events.Any(e => string.Equals(e.Key, definition.Key, StringComparison.Ordinal)))
        {
            return Result.Failure(
                new Error("scenario.duplicate_event", $"Event '{definition.Key}' is already defined."));
        }

        _events.Add(definition);

        return Result.Success();
    }

    public Result AddIssue(IssueDefinition issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var frozen = EnsureEditable();

        if (frozen.IsFailure)
        {
            return frozen;
        }

        if (_issues.Any(i => string.Equals(i.Key, issue.Key, StringComparison.Ordinal)))
        {
            return Result.Failure(
                new Error("scenario.duplicate_issue", $"Issue '{issue.Key}' is already defined."));
        }

        _issues.Add(issue);

        return Result.Success();
    }

    public Result AddParty(PartyId party)
    {
        var frozen = EnsureEditable();

        if (frozen.IsFailure)
        {
            return frozen;
        }

        if (_parties.Contains(party))
        {
            return Result.Failure(
                new Error("scenario.duplicate_party", $"Party {party} is already in this scenario."));
        }

        _parties.Add(party);

        return Result.Success();
    }

    public Result AddInitialVariable(WorldVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);

        var frozen = EnsureEditable();

        if (frozen.IsFailure)
        {
            return frozen;
        }

        _initialVariables.Add(variable);

        return Result.Success();
    }

    /// <summary>
    /// Freezes this version, refusing to publish content that does not validate. Publishing
    /// invalid content would put the failure in a running game rather than in front of the
    /// author.
    /// </summary>
    public Result Publish(ContentValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        if (IsPublished)
        {
            return Result.Failure(
                new Error("scenario.already_published", $"{Name} v{Version} is already published."));
        }

        if (_parties.Count == 0)
        {
            return Result.Failure(
                new Error("scenario.no_parties", "A scenario needs at least one party."));
        }

        var violations = validator.Validate(Events, Issues);

        if (violations.Count > 0)
        {
            return Result.Failure(
                new Error(
                    "scenario.invalid_content",
                    $"{violations.Count} content violations. First: {violations[0]}"));
        }

        IsPublished = true;

        return Result.Success();
    }

    /// <summary>
    /// Opens a new editable version, leaving the published one untouched. This is the only
    /// way to change a scenario that games are already running on.
    /// </summary>
    public Result<ScenarioDefinition> CreateNextVersion()
    {
        if (!IsPublished)
        {
            return Result.Failure<ScenarioDefinition>(
                new Error(
                    "scenario.not_published",
                    "Edit this draft instead of branching from an unpublished version."));
        }

        return Result.Success(
            new ScenarioDefinition(
                Id, Name, Version + 1, _events, _issues, _parties, _initialVariables));
    }

    private Result EnsureEditable() =>
        IsPublished
            ? Result.Failure(
                new Error(
                    "scenario.published_is_immutable",
                    $"{Name} v{Version} is published. Create the next version to make changes."))
            : Result.Success();
}
