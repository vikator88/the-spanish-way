using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Simulation.Enumerations;

using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Content.Services;

/// <summary>
/// Supplies the things a role could be filled with.
/// <para>
/// An abstraction because parties and characters arrive in later slices; the binder's rules
/// do not depend on how they are stored. Implementations must return candidates in a stable
/// order, since the choice is drawn from a seeded stream and order is part of that.
/// </para>
/// </summary>
public interface IRoleCandidateSource
{
    /// <summary>Every instance of this kind, in identifier order.</summary>
    IReadOnlyList<string> Candidates(TargetType kind);

    /// <summary>
    /// Instances of this kind that belong to <paramref name="ownerInstance"/> — the members
    /// of a party, say — in identifier order.
    /// </summary>
    IReadOnlyList<string> CandidatesWithin(TargetType kind, string ownerInstance);

    /// <summary>The instance a built-in role refers to, or a failure when there is none.</summary>
    Result<string> ResolveBuiltIn(RoleName role);
}

/// <summary>
/// Fills a definition's roles with concrete instances when it fires.
/// <para>
/// A role whose selection rule matches nothing makes the definition ineligible. That one
/// rule does the work of a great many hand-written trigger conditions: a leadership
/// challenge cannot fire in a party with no ambitious deputy, and nobody has to remember to
/// write that down.
/// </para>
/// </summary>
public sealed class RoleBinder(IRoleCandidateSource candidates, ConditionEvaluator evaluator)
{
    private readonly IRoleCandidateSource _candidates = candidates;
    private readonly ConditionEvaluator _evaluator = evaluator;

    /// <summary>
    /// Binds every declared role, drawing each choice from the <c>bindings</c> stream.
    /// Scoped roles are bound after the roles they depend on, so <c>$figure</c> is chosen
    /// from the members of whichever party <c>$accused</c> turned out to be.
    /// </summary>
    public Result<RoleBindings> Bind(
        IReadOnlyList<RoleDeclaration> declarations,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(random);

        var ordered = Order(declarations);

        if (ordered.IsFailure)
        {
            return Result.Failure<RoleBindings>(ordered.Error);
        }

        var bindings = RoleBindings.Empty;

        foreach (var declaration in ordered.Value)
        {
            var pool = PoolFor(declaration, bindings);

            if (pool.IsFailure)
            {
                return Result.Failure<RoleBindings>(pool.Error);
            }

            var eligible = new List<string>();

            foreach (var candidate in pool.Value)
            {
                var trial = bindings.With(declaration.Name, candidate);
                var matches = _evaluator.Evaluate(declaration.Selection, trial);

                // A candidate whose attributes cannot be read simply does not qualify;
                // one bad candidate must not make the whole definition unusable.
                if (matches.IsSuccess && matches.Value)
                {
                    eligible.Add(candidate);
                }
            }

            if (eligible.Count == 0)
            {
                return Result.Failure<RoleBindings>(
                    new Error(
                        "role.no_candidate",
                        $"No candidate satisfies role '{declaration.Name}', so the definition is not eligible."));
            }

            bindings = bindings.With(declaration.Name, random.Pick(RandomStream.Bindings, eligible));
        }

        return Result.Success(bindings);
    }

    private Result<IReadOnlyList<string>> PoolFor(RoleDeclaration declaration, RoleBindings bound)
    {
        if (declaration.ScopedTo is not { } owner)
        {
            return Result.Success(_candidates.Candidates(declaration.Kind));
        }

        if (owner.IsBuiltIn)
        {
            var builtIn = _candidates.ResolveBuiltIn(owner);

            return builtIn.IsFailure
                ? Result.Failure<IReadOnlyList<string>>(builtIn.Error)
                : Result.Success(_candidates.CandidatesWithin(declaration.Kind, builtIn.Value));
        }

        var ownerInstance = bound.Resolve(owner);

        return ownerInstance.IsFailure
            ? Result.Failure<IReadOnlyList<string>>(ownerInstance.Error)
            : Result.Success(_candidates.CandidatesWithin(declaration.Kind, ownerInstance.Value));
    }

    /// <summary>
    /// Unscoped roles first, then scoped ones in dependency order. Detects a cycle rather
    /// than looping, because content can always be written wrong.
    /// </summary>
    private static Result<IReadOnlyList<RoleDeclaration>> Order(IReadOnlyList<RoleDeclaration> declarations)
    {
        var remaining = declarations.OrderBy(d => d.Name).ToList();
        var ordered = new List<RoleDeclaration>();
        var bound = new HashSet<RoleName>(RoleName.BuiltIn);

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(d => d.ScopedTo is not { } owner || bound.Contains(owner))
                .ToList();

            if (ready.Count == 0)
            {
                return Result.Failure<IReadOnlyList<RoleDeclaration>>(
                    new Error(
                        "role.circular_scope",
                        "Roles depend on each other in a cycle: "
                        + string.Join(", ", remaining.Select(d => d.Name))));
            }

            foreach (var declaration in ready)
            {
                ordered.Add(declaration);
                bound.Add(declaration.Name);
                remaining.Remove(declaration);
            }
        }

        return Result.Success<IReadOnlyList<RoleDeclaration>>(ordered);
    }
}
