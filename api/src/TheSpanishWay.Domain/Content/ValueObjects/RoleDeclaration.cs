using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Simulation.Enumerations;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Content.ValueObjects;

/// <summary>
/// Declares a slot and the rule for filling it: "<c>$accused</c> is a party with credibility
/// below 60", "<c>$figure</c> is a member of <c>$accused</c> who leads or speaks for it".
/// <para>
/// Selection rules replace a great many hand-written trigger conditions, because a role with
/// no candidate makes the whole definition ineligible — the event simply cannot happen when
/// there is nobody for it to happen to.
/// </para>
/// </summary>
public sealed record RoleDeclaration(
    RoleName Name,
    TargetType Kind,
    ConditionGroup Selection,
    RoleName? ScopedTo)
{
    public static Result<RoleDeclaration> Create(
        RoleName name,
        TargetType kind,
        ConditionGroup? selection = null,
        RoleName? scopedTo = null)
    {
        if (name.IsBuiltIn)
        {
            return Result.Failure<RoleDeclaration>(
                new Error(
                    "role.redeclares_builtin",
                    $"Role '{name}' is built in and cannot be redeclared."));
        }

        if (kind.IsComputed())
        {
            return Result.Failure<RoleDeclaration>(
                new Error(
                    "role.computed_kind",
                    $"Role '{name}' cannot bind to {kind}, which the simulation computes."));
        }

        return Result.Success(
            new RoleDeclaration(name, kind, selection ?? ConditionGroup.Always, scopedTo));
    }

    /// <summary>True when this role can only be filled from within another role's scope.</summary>
    public bool IsScoped => ScopedTo is not null;
}

/// <summary>
/// What each role was bound to when a definition fired, stored on the occurrence.
/// <para>
/// Kept with the occurrence rather than recomputed, so a saved game can explain who the
/// scandal was about without re-running the selection rules against state that has since
/// moved on.
/// </para>
/// </summary>
public sealed record RoleBindings(IReadOnlyDictionary<RoleName, string> Bindings)
{
    public static RoleBindings Empty { get; } = new(new Dictionary<RoleName, string>());

    public bool IsBound(RoleName role) => Bindings.ContainsKey(role);

    public Result<string> Resolve(RoleName role)
    {
        if (!Bindings.TryGetValue(role, out var instance))
        {
            return Result.Failure<string>(
                new Error("role.unbound", $"Role '{role}' is not bound in this occurrence."));
        }

        return Result.Success(instance);
    }

    /// <summary>
    /// Replaces a role token in an instance reference with what it was bound to. A literal
    /// instance passes through untouched.
    /// </summary>
    public Result<string> ResolveInstance(string instance)
    {
        if (!RoleName.IsRoleToken(instance))
        {
            return Result.Success(instance);
        }

        var name = RoleName.Create(instance);

        return name.IsFailure ? Result.Failure<string>(name.Error) : Resolve(name.Value);
    }

    public RoleBindings With(RoleName role, string instance) =>
        new(new Dictionary<RoleName, string>(Bindings) { [role] = instance });
}
