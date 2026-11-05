using System.Text.Json;
using System.Text.Json.Serialization;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.World.ValueObjects;

namespace TheSpanishWay.Infrastructure.Content;

// The JSON shapes. Deliberately dumb records that mirror the file format rather than the
// domain model: the loader's job is to translate between them, and letting the file format
// dictate the domain types is how content formats end up fossilised.

internal sealed record IssueJson(
    string Key,
    string Kind,
    Dictionary<string, decimal>? AxisLoadings,
    List<string>? LinkedVariables);

internal sealed record ConditionJson(
    string Target, string Instance, string Attribute, string Operator, decimal? Value, decimal? UpperValue);

internal sealed record EffectJson(
    string Target,
    string Instance,
    string Attribute,
    string Operation,
    decimal? Value,
    decimal? MinimumValue,
    decimal? MaximumValue,
    int? DelayDays,
    int? DurationDays,
    string? StackingPolicy);

internal sealed record RoleJson(string Name, string Kind, string? ScopedTo, List<ConditionJson>? Selection);

internal sealed record EventJson(
    string Key,
    string Category,
    decimal BaseProbability,
    bool IsBlocking,
    string? DefaultOption,
    int? DurationDays,
    int? CooldownDays,
    List<RoleJson>? Roles,
    List<ConditionJson>? Triggers,
    List<EffectJson>? Effects,
    List<string>? Tags);

internal sealed record ScenarioJson(
    string Key,
    string Name,
    List<IssueJson>? Issues,
    List<EventJson>? Events);

/// <summary>What came out of a content file, and everything wrong with it.</summary>
public sealed record LoadedScenario(
    string Key,
    string Name,
    IReadOnlyList<IssueDefinition> Issues,
    IReadOnlyList<GameEventDefinition> Events,
    IReadOnlyList<ContentViolation> Violations)
{
    public bool IsValid => Violations.Count == 0;
}

/// <summary>
/// Reads scenario content from JSON.
/// <para>
/// Lives in Infrastructure because the Domain compiles against the BCL alone and an
/// architecture test forbids <c>System.Text.Json</c> there. That is not a technicality: the
/// file format is an infrastructure concern, and the domain model should outlive any
/// particular way of writing it down.
/// </para>
/// <para>
/// Everything it produces is checked against the attribute registry before it is returned, so
/// a typo in a content file fails at load rather than doing nothing at runtime.
/// </para>
/// </summary>
public sealed class JsonScenarioLoader(ContentValidator validator)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ContentValidator _validator = validator;

    public Result<LoadedScenario> Load(string json)
    {
        ScenarioJson? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<ScenarioJson>(json, Options);
        }
        catch (JsonException exception)
        {
            return Result.Failure<LoadedScenario>(
                new Error("content.malformed_json", $"Scenario JSON could not be parsed: {exception.Message}"));
        }

        if (parsed is null)
        {
            return Result.Failure<LoadedScenario>(
                new Error("content.empty_scenario", "Scenario JSON was empty."));
        }

        var violations = new List<ContentViolation>();
        var issues = new List<IssueDefinition>();
        var issueIds = new Dictionary<string, IssueId>(StringComparer.OrdinalIgnoreCase);

        foreach (var issueJson in parsed.Issues ?? [])
        {
            var issue = BuildIssue(issueJson, violations);

            if (issue is not null)
            {
                issues.Add(issue);
                issueIds[issue.Key] = issue.Id;
            }
        }

        var eventIds = (parsed.Events ?? [])
            .ToDictionary(e => e.Key, e => EventDefinitionId.From(DeterministicId(e.Key)), StringComparer.OrdinalIgnoreCase);

        var events = new List<GameEventDefinition>();

        foreach (var eventJson in parsed.Events ?? [])
        {
            var definition = BuildEvent(eventJson, eventIds, violations);

            if (definition is not null)
            {
                events.Add(definition);
            }
        }

        // The registry has the final word, whatever the file's shape allowed.
        violations.AddRange(_validator.Validate(events, issues));

        return Result.Success(
            new LoadedScenario(parsed.Key, parsed.Name, issues, events, violations));
    }

    private static IssueDefinition? BuildIssue(IssueJson json, List<ContentViolation> violations)
    {
        var id = IssueId.From(DeterministicId(json.Key));

        var result = Enum.TryParse<IssueKind>(json.Kind, ignoreCase: true, out var kind) && kind == IssueKind.Positional
            ? IssueDefinition.CreatePositional(
                id, json.Key,
                [.. (json.AxisLoadings ?? []).Select(l =>
                    new AxisLoading(Enum.Parse<IdeologicalAxis>(l.Key, ignoreCase: true), l.Value))])
            : IssueDefinition.CreateValence(
                id, json.Key,
                [.. (json.LinkedVariables ?? []).Select(VariableKey.From)]);

        if (result.IsFailure)
        {
            violations.Add(new ContentViolation(json.Key, result.Error.Code, result.Error.Message));

            return null;
        }

        return result.Value;
    }

    private static GameEventDefinition? BuildEvent(
        EventJson json,
        Dictionary<string, EventDefinitionId> eventIds,
        List<ContentViolation> violations)
    {
        var roles = new List<RoleDeclaration>();

        foreach (var roleJson in json.Roles ?? [])
        {
            var name = RoleName.Create(roleJson.Name);

            if (name.IsFailure)
            {
                violations.Add(new ContentViolation(json.Key, name.Error.Code, name.Error.Message));

                continue;
            }

            var declaration = RoleDeclaration.Create(
                name.Value,
                Enum.Parse<TargetType>(roleJson.Kind, ignoreCase: true),
                BuildConditions(roleJson.Selection),
                roleJson.ScopedTo is null ? null : RoleName.From(roleJson.ScopedTo));

            if (declaration.IsFailure)
            {
                violations.Add(new ContentViolation(json.Key, declaration.Error.Code, declaration.Error.Message));

                continue;
            }

            roles.Add(declaration.Value);
        }

        var definition = GameEventDefinition.Create(
            eventIds[json.Key],
            json.Key,
            Enum.Parse<EventCategory>(json.Category, ignoreCase: true),
            json.BaseProbability,
            BuildConditions(json.Triggers),
            roles,
            [.. (json.Effects ?? []).Select(BuildEffect)],
            json.IsBlocking,
            json.DefaultOption,
            json.DurationDays,
            json.CooldownDays,
            tags: json.Tags is null ? null : new HashSet<string>(json.Tags));

        if (definition.IsFailure)
        {
            violations.Add(new ContentViolation(json.Key, definition.Error.Code, definition.Error.Message));

            return null;
        }

        return definition.Value;
    }

    private static ConditionGroup BuildConditions(List<ConditionJson>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return ConditionGroup.Always;
        }

        return ConditionGroup.All(
            [.. conditions.Select(c =>
            {
                var target = AttributeRef.From(
                    Enum.Parse<TargetType>(c.Target, ignoreCase: true), c.Instance, c.Attribute);

                var op = Enum.Parse<ComparisonOperator>(c.Operator, ignoreCase: true);

                return op switch
                {
                    ComparisonOperator.InRange =>
                        Condition.InRange(target, c.Value ?? 0m, c.UpperValue ?? 0m).Value,
                    ComparisonOperator.Exists => Condition.Exists(target),
                    ComparisonOperator.NotExists => Condition.NotExists(target),
                    _ => Condition.Compare(target, op, c.Value ?? 0m).Value,
                };
            })]);
    }

    private static EffectDefinition BuildEffect(EffectJson json)
    {
        var target = AttributeRef.From(
            Enum.Parse<TargetType>(json.Target, ignoreCase: true), json.Instance, json.Attribute);

        var operation = Enum.Parse<EffectOperation>(json.Operation, ignoreCase: true);

        // A range is rolled once, when the effect is instantiated; a fixed value needs no roll.
        ValueExpression value = json.MinimumValue is { } min && json.MaximumValue is { } max
            ? new ValueExpression.Range(min, max)
            : new ValueExpression.Fixed(json.Value ?? 0m);

        return new EffectDefinition(
            target, operation, value, json.DelayDays, json.DurationDays, null,
            json.StackingPolicy is null
                ? StackingPolicy.RefreshDuration
                : Enum.Parse<StackingPolicy>(json.StackingPolicy, ignoreCase: true),
            null, ConditionGroup.Always);
    }

    /// <summary>
    /// Derives an id from a content key, so the same file always produces the same ids.
    /// A loader minting fresh guids would give every restart a different scenario.
    /// </summary>
    private static Guid DeterministicId(string key)
    {
        var seed = RandomSeed.FromText(key);

        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..8], seed.Value);
        BitConverter.TryWriteBytes(bytes[8..], RandomSeed.FromText(key + "#salt").Value);

        return new Guid(bytes);
    }
}
