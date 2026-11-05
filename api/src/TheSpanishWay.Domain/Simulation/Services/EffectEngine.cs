using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Entities;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.Domain.World.Entities;
using TheSpanishWay.Domain.World.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Simulation.Services;

/// <summary>What the engine writes through: the parts of the world an effect can reach.</summary>
public sealed record EffectContext(
    GameDate CurrentDate,
    WorldState World,
    ModifierStore Modifiers,
    PendingEffectQueue Pending,
    IIdentifierSource Identifiers);

/// <summary>
/// Turns instructions into state changes, all or nothing.
/// <para>
/// Effects first produce a list of proposed changes; the engine validates every one of them
/// and only then applies any. A failure anywhere rejects the whole batch with nothing
/// written, which is how "a failed action must not partially mutate the game state" is
/// actually achieved rather than merely intended. See design D5.
/// </para>
/// </summary>
public sealed class EffectEngine(AttributeRegistry registry)
{
    private readonly AttributeRegistry _registry = registry;

    /// <summary>
    /// Applies a batch. Returns a failure, having written nothing, if any instruction is
    /// invalid; otherwise applies all of them and reports any clamp truncation.
    /// </summary>
    public Result<EffectApplicationResult> Apply(
        IReadOnlyList<EffectInstruction> instructions,
        EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(context);

        var rejections = Validate(instructions, context);

        if (rejections.Count > 0)
        {
            // Name the first offender: a batch is usually wrong for one reason, and the
            // whole list is noise when the author only needs to fix the typo.
            return Result.Failure<EffectApplicationResult>(
                new Error(
                    "effect_engine.change_set_rejected",
                    $"{rejections.Count} of {instructions.Count} effects were invalid. "
                    + $"First: {rejections[0]}"));
        }

        var baseMutations = new List<AttributeRef>();
        var modifiersAdded = new List<Modifier>();
        var queued = new List<PendingEffect>();
        var diagnostics = new List<ClampDiagnostic>();

        foreach (var instruction in instructions)
        {
            if (instruction.IsDelayed)
            {
                queued.Add(Queue(instruction, context));

                continue;
            }

            if (instruction.IsTemporary)
            {
                var modifier = AddModifier(instruction, context);

                if (modifier is not null)
                {
                    modifiersAdded.Add(modifier);
                }

                continue;
            }

            var mutation = MutateBase(instruction, context, diagnostics);

            if (mutation)
            {
                baseMutations.Add(instruction.Target);
            }
        }

        return Result.Success(
            new EffectApplicationResult(baseMutations, modifiersAdded, queued, diagnostics));
    }

    /// <summary>
    /// Applies every queued effect due on this date, rescheduling the recurring ones.
    /// Ordered by apply date then sequence, so two effects due the same day always land in
    /// the same order.
    /// </summary>
    public Result<EffectApplicationResult> ApplyDue(EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var due = context.Pending.DueOn(context.CurrentDate);

        if (due.Count == 0)
        {
            return Result.Success(EffectApplicationResult.Empty);
        }

        var instructions = due
            .Select(p => new EffectInstruction(
                p.Target, p.Operation, p.Value, null, p.DurationDays, null,
                p.StackKey, p.Policy, p.Provenance))
            .ToList();

        var applied = Apply(instructions, context);

        if (applied.IsFailure)
        {
            return applied;
        }

        foreach (var effect in due)
        {
            context.Pending.MarkApplied(effect);
        }

        return applied;
    }

    private List<EffectRejection> Validate(
        IReadOnlyList<EffectInstruction> instructions,
        EffectContext context)
    {
        var rejections = new List<EffectRejection>();

        for (var index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];

            var permitted = _registry.ValidateAuthoredWrite(instruction.Target, instruction.Operation);

            if (permitted.IsFailure)
            {
                rejections.Add(new EffectRejection(index, instruction.Target, permitted.Error));

                continue;
            }

            // The attribute may be addressable in principle and still name nothing that
            // exists in this game.
            var resolved = Resolve(instruction.Target, context);

            if (resolved.IsFailure)
            {
                rejections.Add(new EffectRejection(index, instruction.Target, resolved.Error));
            }
        }

        return rejections;
    }

    private static Result<WorldVariable> Resolve(AttributeRef target, EffectContext context)
    {
        if (target.Target != TargetType.WorldVariable)
        {
            // Parties, characters, relations and the rest arrive with their own slices.
            // Until then an effect naming one is rejected rather than silently ignored.
            return Result.Failure<WorldVariable>(
                new Error(
                    "effect_engine.target_not_implemented",
                    $"Effects on {target.Target} are not implemented yet."));
        }

        var key = VariableKey.Create(target.Instance);

        return key.IsFailure
            ? Result.Failure<WorldVariable>(key.Error)
            : context.World.Variable(key.Value);
    }

    private static bool MutateBase(
        EffectInstruction instruction,
        EffectContext context,
        List<ClampDiagnostic> diagnostics)
    {
        var variable = Resolve(instruction.Target, context).Value;

        BoundedChange<decimal> change;

        switch (instruction.Target.Attribute)
        {
            case "target":
                variable.SetTargetValue(instruction.Value);

                return true;

            case "evolutionrate":
                variable.SetEvolutionRate(instruction.Value);

                return true;

            default:
                change = instruction.Operation switch
                {
                    EffectOperation.Set => variable.SetBase(instruction.Value),
                    EffectOperation.Add => variable.AddToBase(instruction.Value),
                    EffectOperation.Multiply => variable.MultiplyBase(instruction.Value),
                    _ => variable.AddToBase(instruction.Value),
                };

                break;
        }

        if (change.WasTruncated)
        {
            // Reported, not discarded: the game can explain that bounds refused part of it.
            diagnostics.Add(
                new ClampDiagnostic(
                    instruction.Target,
                    instruction.Value,
                    change.Result.Value,
                    change.Truncated,
                    instruction.Provenance));
        }

        return true;
    }

    private static Modifier? AddModifier(EffectInstruction instruction, EffectContext context)
    {
        var expiry = instruction.DurationDays is { } days
            ? context.CurrentDate.AddDays(days)
            : (GameDate?)null;

        var modifier = new Modifier(
            context.Identifiers.NextModifierId(),
            instruction.Target,
            instruction.Operation,
            instruction.Value,
            expiry,
            instruction.StackKey,
            instruction.Policy,
            instruction.Provenance,
            context.Modifiers.NextSequence());

        return context.Modifiers.Add(modifier);
    }

    private static PendingEffect Queue(EffectInstruction instruction, EffectContext context)
    {
        var effect = new PendingEffect(
            context.Identifiers.NextPendingEffectId(),
            instruction.Target,
            instruction.Operation,
            instruction.Value,
            context.CurrentDate.AddDays(instruction.DelayDays ?? 0),
            instruction.DurationDays,
            instruction.Recurrence,
            instruction.StackKey,
            instruction.Policy,
            instruction.Provenance,
            PendingEffectStatus.Scheduled);

        context.Pending.Enqueue(effect);

        return effect;
    }
}
