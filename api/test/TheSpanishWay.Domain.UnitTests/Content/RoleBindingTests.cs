using Shouldly;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Content;

/// <summary>
/// Roles are what make thirty event definitions reusable rather than thirty one-offs.
/// </summary>
public sealed class RoleBindingTests
{
    private static readonly RoleName Accused = RoleName.From("$accused");
    private static readonly RoleName Figure = RoleName.From("$figure");

    /// <summary>
    /// The worked example from the design: a party with credibility below 60, and a leading
    /// figure within it.
    /// </summary>
    private static FakeWorld ScandalWorld() =>
        new FakeWorld()
            .WithCandidates(TargetType.Party, "party-a", "party-b", "party-c")
            .WithValue(TargetType.Party, "party-a", "credibility", 45m)
            .WithValue(TargetType.Party, "party-b", "credibility", 80m)
            .WithValue(TargetType.Party, "party-c", "credibility", 90m)
            .WithMembers("party-a", "char-1", "char-2")
            .WithValue(TargetType.Character, "char-1", "popularity", 70m)
            .WithValue(TargetType.Character, "char-2", "popularity", 20m);

    private static (RoleBinder Binder, FakeWorld World) Build(FakeWorld? world = null)
    {
        var fake = world ?? ScandalWorld();

        return (new RoleBinder(fake, new ConditionEvaluator(fake)), fake);
    }

    private static RoleDeclaration AccusedParty(decimal credibilityBelow = 60m) =>
        RoleDeclaration.Create(
            Accused,
            TargetType.Party,
            ConditionGroup.All(
                Condition.Compare(
                    AttributeRef.From(TargetType.Party, "$accused", "credibility"),
                    ComparisonOperator.LessThan, credibilityBelow).Value)).Value;

    private static RoleDeclaration FigureWithin() =>
        RoleDeclaration.Create(
            Figure,
            TargetType.Character,
            ConditionGroup.All(
                Condition.Compare(
                    AttributeRef.From(TargetType.Character, "$figure", "popularity"),
                    ComparisonOperator.GreaterThan, 50m).Value),
            scopedTo: Accused).Value;

    [Fact]
    public void A_scandal_binds_a_party_and_a_character_within_it()
    {
        var (binder, _) = Build();

        var bindings = binder.Bind([AccusedParty(), FigureWithin()], new ScriptedRandom());

        bindings.IsSuccess.ShouldBeTrue();
        bindings.Value.Resolve(Accused).Value.ShouldBe("party-a", "the only party below 60 credibility");
        bindings.Value.Resolve(Figure).Value.ShouldBe("char-1", "the only member popular enough");
    }

    [Fact]
    public void A_role_with_no_candidate_makes_the_definition_ineligible()
    {
        // The rule that replaces a great many hand-written trigger conditions.
        var (binder, _) = Build();

        var result = binder.Bind([AccusedParty(credibilityBelow: 10m)], new ScriptedRandom());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("role.no_candidate");
    }

    [Fact]
    public void A_scoped_role_draws_only_from_its_owners_members()
    {
        var world = ScandalWorld().WithMembers("party-b", "char-9");
        var (binder, _) = Build(world);

        var bindings = binder.Bind([AccusedParty(), FigureWithin()], new ScriptedRandom());

        bindings.Value.Resolve(Figure).Value.ShouldBe("char-1", "char-9 belongs to a different party");
    }

    [Fact]
    public void The_choice_among_several_candidates_comes_from_the_bindings_stream()
    {
        var world = new FakeWorld()
            .WithCandidates(TargetType.Party, "party-a", "party-b", "party-c")
            .WithValue(TargetType.Party, "party-a", "credibility", 10m)
            .WithValue(TargetType.Party, "party-b", "credibility", 20m)
            .WithValue(TargetType.Party, "party-c", "credibility", 30m);

        var (binder, _) = Build(world);

        // Three eligible candidates; a draw of 0.9 lands on the last.
        var bindings = binder.Bind(
            [AccusedParty()], new ScriptedRandom().Script(RandomStream.Bindings, 0.9m));

        bindings.Value.Resolve(Accused).Value.ShouldBe("party-c");
    }

    [Fact]
    public void Binding_is_reproducible()
    {
        var (binder, _) = Build();

        var first = binder.Bind([AccusedParty()], new ScriptedRandom().Script(RandomStream.Bindings, 0.3m));
        var second = binder.Bind([AccusedParty()], new ScriptedRandom().Script(RandomStream.Bindings, 0.3m));

        first.Value.Resolve(Accused).Value.ShouldBe(second.Value.Resolve(Accused).Value);
    }

    [Fact]
    public void A_builtin_role_cannot_be_redeclared()
    {
        RoleDeclaration.Create(RoleName.Player, TargetType.Party)
            .Error.Code.ShouldBe("role.redeclares_builtin");
    }

    [Fact]
    public void A_role_cannot_bind_to_a_computed_target()
    {
        RoleDeclaration.Create(RoleName.From("$x"), TargetType.SegmentPerception)
            .Error.Code.ShouldBe("role.computed_kind");
    }

    [Fact]
    public void Circular_role_scoping_is_detected_rather_than_looped()
    {
        var a = RoleDeclaration.Create(RoleName.From("$a"), TargetType.Character,
            scopedTo: RoleName.From("$b")).Value;
        var b = RoleDeclaration.Create(RoleName.From("$b"), TargetType.Character,
            scopedTo: RoleName.From("$a")).Value;

        var (binder, _) = Build();

        binder.Bind([a, b], new ScriptedRandom()).Error.Code.ShouldBe("role.circular_scope");
    }

    [Fact]
    public void Built_in_roles_need_no_declaration()
    {
        RoleName.Player.IsBuiltIn.ShouldBeTrue();
        RoleName.GovernmentLead.IsBuiltIn.ShouldBeTrue();
        RoleName.Self.IsBuiltIn.ShouldBeTrue();
    }

    [Fact]
    public void A_role_name_must_carry_its_sigil()
    {
        RoleName.Create("accused").Error.Code.ShouldBe("role.missing_sigil");
        RoleName.IsRoleToken("$accused").ShouldBeTrue();
        RoleName.IsRoleToken("party-a").ShouldBeFalse();
    }
}
