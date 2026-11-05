using TheSpanishWay.Domain.Actions.Entities;
using TheSpanishWay.Domain.Actions.Enumerations;
using TheSpanishWay.Domain.Actions.ValueObjects;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Services;
using TheSpanishWay.Domain.Simulation.ValueObjects;

namespace TheSpanishWay.Domain.Actions.Services;

/// <summary>What one executed action produced.</summary>
public sealed record ActionOutcome(
    ActionExecution Execution,
    EffectApplicationResult Effects,
    IReadOnlyList<Stimulus> QueuedStimuli);

/// <summary>
/// Executes an action: validate everything, then pay, then apply, then queue the stimuli.
/// <para>
/// The order is the point. Nothing is written until the whole thing is known to be
/// executable, so a rejected action leaves the snapshot byte-identical. Costs and effects
/// commit together or not at all.
/// </para>
/// <para>
/// One handler per category rather than one class per action: the specifics are data.
/// </para>
/// </summary>
public sealed class ActionExecutionService(
    ActionAvailabilityPolicy availability,
    EffectInstantiator instantiator,
    EffectEngine engine)
{
    private readonly ActionAvailabilityPolicy _availability = availability;
    private readonly EffectInstantiator _instantiator = instantiator;
    private readonly EffectEngine _engine = engine;

    public Result<ActionOutcome> Execute(
        ActionDefinition definition,
        ActionAvailabilityContext context,
        EffectContext effects,
        IReadOnlyDictionary<string, string> parameters,
        ActionExecutionId executionId,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(random);

        var permitted = _availability.CanExecute(definition, context);

        if (permitted.IsFailure)
        {
            return Result.Failure<ActionOutcome>(permitted.Error);
        }

        var validParameters = ValidateParameters(definition, parameters);

        if (validParameters.IsFailure)
        {
            return Result.Failure<ActionOutcome>(validParameters.Error);
        }

        var execution = ActionExecution.Executed(
            executionId, definition.Id, context.Actor.Id, context.Today, parameters, definition.Key);

        var instructions = _instantiator.Instantiate(
            definition.Effects, RoleBindings.Empty, execution.Provenance, random, definition.Key);

        if (instructions.IsFailure)
        {
            return Result.Failure<ActionOutcome>(instructions.Error);
        }

        // Dry-run the effects before paying. The engine validates the whole change set and
        // writes nothing if any part of it is invalid, so a failure here costs the party
        // nothing at all.
        var applied = _engine.Apply(instructions.Value, effects);

        if (applied.IsFailure)
        {
            return Result.Failure<ActionOutcome>(applied.Error);
        }

        Pay(definition.Cost, context);

        context.Ledger.RecordExecution(
            context.Actor.Id, definition.Id, context.Today, context.GameStart,
            definition.CountsAgainstAllowance);

        // Stimuli are queued rather than processed now: media and reactions run at one fixed
        // point in the next day's loop, never scattered across the places that raise them.
        var stimuli = definition.Stimuli
            .Select(t => ToStimulus(t, context))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        return Result.Success(new ActionOutcome(execution, applied.Value, stimuli));
    }

    private static void Pay(ActionCost cost, ActionAvailabilityContext context)
    {
        if (cost.IsFree)
        {
            return;
        }

        context.Actor.Spend(cost.Budget);
        context.Actor.AdjustCohesion(-cost.Cohesion);
        context.Actor.AdjustCredibility(-cost.Credibility);
    }

    private static Stimulus? ToStimulus(StimulusTemplate template, ActionAvailabilityContext context)
    {
        var result = template.Kind == Simulation.Enumerations.StimulusKind.Positional
            ? Stimulus.Positional(
                context.Actor.Id, template.Issue, template.Stance ?? 0m,
                template.Intensity, template.Visibility)
            : Stimulus.Valence(
                context.Actor.Id, template.Issue, template.Intensity, template.Visibility);

        return result.IsSuccess ? result.Value : null;
    }

    private static Result ValidateParameters(
        ActionDefinition definition,
        IReadOnlyDictionary<string, string> parameters)
    {
        foreach (var required in definition.ParameterSchema)
        {
            if (!parameters.ContainsKey(required))
            {
                return Result.Failure(
                    new Error(
                        "action.missing_parameter",
                        $"'{definition.Key}' requires the parameter '{required}'."));
            }
        }

        return Result.Success();
    }
}
