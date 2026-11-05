using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Domain.Actions.ValueObjects;

/// <summary>
/// What an action costs to take.
/// <para>
/// Validated and paid atomically with the action's effects: either the party pays and the
/// effects land, or neither happens. A half-paid action is the bug this exists to prevent.
/// </para>
/// </summary>
public sealed record ActionCost(decimal Budget, decimal Cohesion, decimal Credibility)
{
    public static ActionCost Free { get; } = new(0m, 0m, 0m);

    public bool IsFree => Budget == 0m && Cohesion == 0m && Credibility == 0m;

    public static Result<ActionCost> Create(decimal budget, decimal cohesion = 0m, decimal credibility = 0m)
    {
        if (budget < 0m || cohesion < 0m || credibility < 0m)
        {
            return Result.Failure<ActionCost>(
                new Error("action_cost.negative", "An action cost cannot be negative."));
        }

        return Result.Success(new ActionCost(budget, cohesion, credibility));
    }
}
