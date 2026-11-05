using TheSpanishWay.Domain.Actions.Entities;
using TheSpanishWay.Domain.Actions.ValueObjects;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Games.Enumerations;
using TheSpanishWay.Domain.Politics.Entities;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Actions.Services;

/// <summary>Everything the policy needs to judge whether an action may be taken now.</summary>
public sealed record ActionAvailabilityContext(
    PartyState Actor,
    GameDate Today,
    GameDate GameStart,
    GameStatus Status,
    ActionLedger Ledger);

/// <summary>
/// Whether a party may take an action right now.
/// <para>
/// Everything is checked before anything is written. An action that fails here has touched
/// nothing, which is what "a failed action must not partially mutate the game state"
/// requires in practice.
/// </para>
/// </summary>
public sealed class ActionAvailabilityPolicy(ConditionEvaluator evaluator)
{
    private readonly ConditionEvaluator _evaluator = evaluator;

    public Result CanExecute(ActionDefinition definition, ActionAvailabilityContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        // Answering a scandal with a press conference is a voluntary action, so
        // AwaitingDecision permits actions even though it blocks the calendar.
        if (context.Status is not (GameStatus.Running or GameStatus.AwaitingDecision))
        {
            return Result.Failure(
                new Error("action.wrong_game_status", $"A game that is {context.Status} accepts no actions."));
        }

        if (!context.Actor.IsActive)
        {
            return Result.Failure(
                new Error("action.inactive_party", $"{context.Actor.Name} is {context.Actor.Status}."));
        }

        if (!context.Ledger.IsOffCooldown(
                context.Actor.Id, definition.Id, definition.CooldownDays, context.Today))
        {
            return Result.Failure(
                new Error("action.on_cooldown", $"'{definition.Key}' is still on cooldown."));
        }

        if (definition.CountsAgainstAllowance
            && context.Ledger.RemainingAllowance(context.Actor.Id, context.Today, context.GameStart) <= 0)
        {
            return Result.Failure(
                new Error(
                    "action.allowance_exhausted",
                    $"{context.Actor.Name} has used its voluntary actions for this week."));
        }

        var affordable = CanAfford(definition.Cost, context.Actor);

        if (affordable.IsFailure)
        {
            return affordable;
        }

        var conditions = _evaluator.Evaluate(definition.AvailabilityConditions, RoleBindings.Empty);

        if (conditions.IsFailure)
        {
            return Result.Failure(conditions.Error);
        }

        return conditions.Value
            ? Result.Success()
            : Result.Failure(
                new Error("action.conditions_unmet", $"'{definition.Key}' is not available right now."));
    }

    /// <summary>
    /// A party may not spend what it does not have. Budget is the hard constraint; cohesion
    /// and credibility are costs an action imposes rather than resources it draws on.
    /// </summary>
    private static Result CanAfford(ActionCost cost, PartyState actor)
    {
        if (cost.Budget > 0m && actor.Budget < cost.Budget)
        {
            return Result.Failure(
                new Error(
                    "action.insufficient_budget",
                    $"{actor.Name} cannot afford {cost.Budget}; it has {actor.Budget}."));
        }

        return Result.Success();
    }
}
