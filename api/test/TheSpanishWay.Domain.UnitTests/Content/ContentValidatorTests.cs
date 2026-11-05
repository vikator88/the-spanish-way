using Shouldly;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.World.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Content;

/// <summary>
/// Content fails at load, not at runtime. The difference is a typo caught in CI versus a
/// scenario that silently does less than its author believed.
/// </summary>
public sealed class ContentValidatorTests
{
    private static readonly ContentValidator Validator = new(AttributeRegistry.Default());

    private static readonly EventDefinitionId Id =
        EventDefinitionId.From(Guid.Parse("0000000e-0000-0000-0000-000000000001"));

    private static AttributeRef Inflation(string attribute = "current") =>
        AttributeRef.From(TargetType.WorldVariable, "inflation", attribute);

    private static GameEventDefinition Event(
        IReadOnlyList<EffectDefinition>? effects = null,
        ConditionGroup? triggers = null,
        IReadOnlyList<RoleDeclaration>? roles = null,
        IReadOnlyList<EventDefinitionId>? followUps = null) =>
        GameEventDefinition.Create(
            Id, "scandal", EventCategory.Random, 1m, triggers, roles, effects,
            followUps: followUps).Value;

    private static IReadOnlyList<ContentViolation> Validate(GameEventDefinition definition) =>
        Validator.Validate([definition], []);

    [Fact]
    public void Valid_content_produces_no_violations()
    {
        Validate(Event(effects: [EffectDefinition.Immediate(Inflation(), EffectOperation.Add, 1m)]))
            .ShouldBeEmpty();
    }

    [Fact]
    public void An_unknown_attribute_fails_validation()
    {
        var violations = Validate(Event(effects:
        [
            EffectDefinition.Immediate(
                AttributeRef.From(TargetType.WorldVariable, "inflation", "no-such-field"),
                EffectOperation.Add, 1m),
        ]));

        violations.ShouldContain(v => v.Code == "attribute_registry.unknown_attribute");
    }

    [Fact]
    public void An_effect_writing_a_computed_attribute_fails_validation()
    {
        var violations = Validate(Event(effects:
        [
            EffectDefinition.Immediate(
                AttributeRef.From(TargetType.SegmentPerception, "$accused", "trust"),
                EffectOperation.Add, -10m),
        ]));

        violations.ShouldContain(v => v.Code == "attribute_registry.computed_attribute");
    }

    [Fact]
    public void An_unpermitted_operation_fails_validation()
    {
        var violations = Validate(Event(effects:
        [
            EffectDefinition.Immediate(Inflation("evolutionrate"), EffectOperation.Multiply, 2m),
        ]));

        violations.ShouldContain(v => v.Code == "attribute_registry.operation_not_permitted");
    }

    [Fact]
    public void A_reusable_definition_naming_a_literal_party_fails_validation()
    {
        // A definition naming a concrete party is a one-off masquerading as content.
        var violations = Validate(Event(effects:
        [
            EffectDefinition.Immediate(
                AttributeRef.From(TargetType.Party, "partido-popular", "cohesion"),
                EffectOperation.Add, -5m),
        ]));

        violations.ShouldContain(v => v.Code == "content.literal_instance");
    }

    [Fact]
    public void An_undeclared_role_fails_validation()
    {
        var violations = Validate(Event(effects:
        [
            EffectDefinition.Immediate(
                AttributeRef.From(TargetType.Party, "$nobody", "cohesion"), EffectOperation.Add, -5m),
        ]));

        violations.ShouldContain(v => v.Code == "role.undeclared");
    }

    [Fact]
    public void A_declared_role_passes_validation()
    {
        var role = RoleDeclaration.Create(RoleName.From("$accused"), TargetType.Party).Value;

        var violations = Validate(Event(
            roles: [role],
            effects:
            [
                EffectDefinition.Immediate(
                    AttributeRef.From(TargetType.Party, "$accused", "cohesion"), EffectOperation.Add, -5m),
            ]));

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void A_built_in_role_needs_no_declaration()
    {
        var violations = Validate(Event(effects:
        [
            EffectDefinition.Immediate(
                AttributeRef.From(TargetType.Party, "$player", "cohesion"), EffectOperation.Add, -5m),
        ]));

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void A_role_scoped_to_an_undeclared_role_fails_validation()
    {
        var figure = RoleDeclaration.Create(
            RoleName.From("$figure"), TargetType.Character, scopedTo: RoleName.From("$missing")).Value;

        Validate(Event(roles: [figure])).ShouldContain(v => v.Code == "role.unknown_scope");
    }

    [Fact]
    public void An_unknown_follow_up_fails_validation()
    {
        var violations = Validate(Event(followUps: [EventDefinitionId.From(Guid.NewGuid())]));

        violations.ShouldContain(v => v.Code == "event.unknown_follow_up");
    }

    [Fact]
    public void Trigger_conditions_are_validated_too()
    {
        var violations = Validate(Event(triggers: ConditionGroup.All(
            Condition.Compare(
                AttributeRef.From(TargetType.WorldVariable, "inflation", "bogus"),
                ComparisonOperator.GreaterThan, 1m).Value)));

        violations.ShouldContain(v => v.Code == "attribute_registry.unknown_attribute");
    }

    [Fact]
    public void Every_violation_is_reported_not_just_the_first()
    {
        var violations = Validate(Event(effects:
        [
            EffectDefinition.Immediate(Inflation("bogus-one"), EffectOperation.Add, 1m),
            EffectDefinition.Immediate(Inflation("bogus-two"), EffectOperation.Add, 1m),
        ]));

        violations.Count.ShouldBeGreaterThanOrEqualTo(2, "an author wants the whole list");
    }

    [Fact]
    public void A_positional_issue_needs_axis_loadings()
    {
        IssueDefinition.CreatePositional(IssueId.From(Guid.NewGuid()), "pensions", [])
            .Error.Code.ShouldBe("issue.no_axis_loadings");
    }

    [Fact]
    public void A_valence_issue_needs_linked_variables()
    {
        IssueDefinition.CreateValence(IssueId.From(Guid.NewGuid()), "corruption", [])
            .Error.Code.ShouldBe("issue.no_linked_variables");
    }

    [Fact]
    public void Axis_loadings_normalise_to_one()
    {
        var issue = IssueDefinition.CreatePositional(
            IssueId.From(Guid.NewGuid()), "pensions",
            [new AxisLoading(IdeologicalAxis.Economy, 3m), new AxisLoading(IdeologicalAxis.SocialValues, 1m)]).Value;

        issue.NormalisedLoadings().Sum(l => l.Weight).ShouldBe(1m);
    }

    [Fact]
    public void A_positional_and_a_valence_issue_are_never_mixed()
    {
        var positional = IssueDefinition.CreatePositional(
            IssueId.From(Guid.NewGuid()), "pensions",
            [new AxisLoading(IdeologicalAxis.Economy, 1m)]).Value;

        var valence = IssueDefinition.CreateValence(
            IssueId.From(Guid.NewGuid()), "corruption", [VariableKey.From("institutionaltrust")]).Value;

        positional.LinkedVariables.ShouldBeEmpty();
        valence.AxisLoadings.ShouldBeEmpty();
        positional.Kind.ShouldBe(IssueKind.Positional);
        valence.Kind.ShouldBe(IssueKind.Valence);
    }
}
