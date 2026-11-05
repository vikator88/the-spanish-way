using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.Content.Services;

/// <summary>One thing wrong with a piece of content, named precisely enough to fix.</summary>
public sealed record ContentViolation(string DefinitionKey, string Code, string Message)
{
    public override string ToString() => $"[{DefinitionKey}] {Code}: {Message}";
}

/// <summary>
/// Checks authored content before a game ever runs.
/// <para>
/// This is the difference between a typo caught in CI and a scenario that silently does less
/// than its author believed. v0.1 is explicit that unsupported attributes and operators must
/// fail configuration validation rather than evaluating quietly to nothing.
/// </para>
/// </summary>
public sealed class ContentValidator(AttributeRegistry registry)
{
    private readonly AttributeRegistry _registry = registry;

    /// <summary>
    /// Every violation, rather than just the first: an author fixing content wants the whole
    /// list, not one error per run.
    /// </summary>
    public IReadOnlyList<ContentViolation> Validate(
        IReadOnlyList<GameEventDefinition> events,
        IReadOnlyList<IssueDefinition> issues)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(issues);

        var violations = new List<ContentViolation>();
        var eventIds = events.Select(e => e.Id).ToHashSet();

        foreach (var issue in issues)
        {
            ValidateIssue(issue, violations);
        }

        foreach (var definition in events.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            ValidateEvent(definition, eventIds, violations);
        }

        return violations;
    }

    private static void ValidateIssue(IssueDefinition issue, List<ContentViolation> violations)
    {
        if (issue.Kind == IssueKind.Positional && issue.AxisLoadings.Count == 0)
        {
            violations.Add(new ContentViolation(
                issue.Key, "issue.no_axis_loadings", "A positional issue must load onto at least one axis."));
        }

        if (issue.Kind == IssueKind.Valence && issue.LinkedVariables.Count == 0)
        {
            violations.Add(new ContentViolation(
                issue.Key, "issue.no_linked_variables", "A valence issue must link to at least one variable."));
        }

        // Loadings that cannot be normalised cannot be weighted against anything.
        if (issue.Kind == IssueKind.Positional && issue.AxisLoadings.Sum(l => l.Weight) <= 0m)
        {
            violations.Add(new ContentViolation(
                issue.Key, "issue.unnormalisable_loadings", "Axis loadings sum to zero and cannot be normalised."));
        }
    }

    private void ValidateEvent(
        GameEventDefinition definition,
        HashSet<Shared.Identifiers.EventDefinitionId> knownEvents,
        List<ContentViolation> violations)
    {
        var declaredRoles = definition.Roles.Select(r => r.Name).ToHashSet();

        foreach (var condition in definition.TriggerConditions.Flatten())
        {
            ValidateAttributeReference(
                definition.Key, condition.Target, declaredRoles, violations, isEffect: false);
        }

        for (var index = 0; index < definition.Effects.Count; index++)
        {
            var effect = definition.Effects[index];

            ValidateAttributeReference(
                definition.Key, effect.Target, declaredRoles, violations, isEffect: true, effect.Operation);

            foreach (var condition in effect.Conditions.Flatten())
            {
                ValidateAttributeReference(
                    definition.Key, condition.Target, declaredRoles, violations, isEffect: false);
            }

            if (effect.Value is ValueExpression.Range range && range.Minimum > range.Maximum)
            {
                violations.Add(new ContentViolation(
                    definition.Key, "effect.inverted_range", $"Effect #{index} has an inverted value range."));
            }
        }

        foreach (var role in definition.Roles)
        {
            foreach (var condition in role.Selection.Flatten())
            {
                ValidateAttributeReference(
                    definition.Key, condition.Target, declaredRoles, violations, isEffect: false, allowRole: role.Name);
            }

            if (role.ScopedTo is { } owner && !owner.IsBuiltIn && !declaredRoles.Contains(owner))
            {
                violations.Add(new ContentViolation(
                    definition.Key, "role.unknown_scope", $"Role '{role.Name}' is scoped to undeclared '{owner}'."));
            }
        }

        foreach (var followUp in definition.FollowUpEvents.Where(f => !knownEvents.Contains(f)))
        {
            violations.Add(new ContentViolation(
                definition.Key, "event.unknown_follow_up", $"Follow-up {followUp} is not a defined event."));
        }

        if (definition.Category == EventCategory.Chained && definition.BaseProbability > 0m
            && definition.FollowUpEvents.Count == 0 && definition.Roles.Count == 0)
        {
            // Not fatal, but a chained event nothing unlocks will never be seen.
            violations.Add(new ContentViolation(
                definition.Key, "event.unreachable_chain",
                "A chained event is only reachable as another event's follow-up."));
        }
    }

    private void ValidateAttributeReference(
        string definitionKey,
        AttributeRef target,
        HashSet<RoleName> declaredRoles,
        List<ContentViolation> violations,
        bool isEffect,
        Simulation.Enumerations.EffectOperation operation = Simulation.Enumerations.EffectOperation.Add,
        RoleName? allowRole = null)
    {
        if (RoleName.IsRoleToken(target.Instance))
        {
            var role = RoleName.Create(target.Instance);

            if (role.IsFailure)
            {
                violations.Add(new ContentViolation(definitionKey, "role.malformed", role.Error.Message));

                return;
            }

            if (!role.Value.IsBuiltIn && !declaredRoles.Contains(role.Value)
                && (allowRole is null || allowRole.Value != role.Value))
            {
                violations.Add(new ContentViolation(
                    definitionKey, "role.undeclared", $"Role '{role.Value}' is used but never declared."));
            }
        }
        else if (target.Target != Shared.Enumerations.TargetType.WorldVariable)
        {
            // A reusable definition naming a concrete party or character is a one-off
            // masquerading as content; it should use a role with a selection rule instead.
            violations.Add(new ContentViolation(
                definitionKey, "content.literal_instance",
                $"'{target.Instance}' is a literal {target.Target} id. Reusable definitions address roles."));
        }

        var lookup = _registry.Lookup(target);

        if (lookup.IsFailure)
        {
            violations.Add(new ContentViolation(definitionKey, lookup.Error.Code, lookup.Error.Message));

            return;
        }

        if (!isEffect)
        {
            return;
        }

        var writable = _registry.ValidateAuthoredWrite(target, operation);

        if (writable.IsFailure)
        {
            violations.Add(new ContentViolation(definitionKey, writable.Error.Code, writable.Error.Message));
        }
    }
}
