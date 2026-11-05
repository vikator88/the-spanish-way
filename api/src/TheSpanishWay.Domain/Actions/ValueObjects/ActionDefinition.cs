using TheSpanishWay.Domain.Actions.Enumerations;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Simulation.ValueObjects;

namespace TheSpanishWay.Domain.Actions.ValueObjects;

/// <summary>
/// What a party can choose to do, described as content.
/// <para>
/// The same definition produces different outcomes in different contexts, because its
/// effects address roles and its stimuli are filtered through media and reactions before
/// reaching anybody's opinion.
/// </para>
/// </summary>
public sealed record ActionDefinition(
    ActionDefinitionId Id,
    string Key,
    ActionCategory Category,
    ConditionGroup AvailabilityConditions,
    ActionCost Cost,
    IReadOnlyList<EffectDefinition> Effects,
    IReadOnlyList<StimulusTemplate> Stimuli,
    int? CooldownDays,
    bool CountsAgainstAllowance,
    IReadOnlySet<string> ParameterSchema,
    IReadOnlySet<string> Tags)
{
    public static Result<ActionDefinition> Create(
        ActionDefinitionId id,
        string key,
        ActionCategory category,
        ActionCost? cost = null,
        ConditionGroup? availability = null,
        IReadOnlyList<EffectDefinition>? effects = null,
        IReadOnlyList<StimulusTemplate>? stimuli = null,
        int? cooldownDays = null,
        bool countsAgainstAllowance = true,
        IReadOnlySet<string>? parameterSchema = null,
        IReadOnlySet<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<ActionDefinition>(
                new Error("action_definition.empty_key", "An action definition needs a key."));
        }

        if (cooldownDays is < 0)
        {
            return Result.Failure<ActionDefinition>(
                new Error("action_definition.negative_cooldown", $"Cooldown for '{key}' cannot be negative."));
        }

        return Result.Success(
            new ActionDefinition(
                id, key.Trim().ToLowerInvariant(), category,
                availability ?? ConditionGroup.Always,
                cost ?? ActionCost.Free,
                effects ?? [], stimuli ?? [], cooldownDays, countsAgainstAllowance,
                parameterSchema ?? new HashSet<string>(),
                tags ?? new HashSet<string>()));
    }
}
