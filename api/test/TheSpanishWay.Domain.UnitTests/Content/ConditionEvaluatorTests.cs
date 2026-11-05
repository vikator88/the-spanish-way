using Shouldly;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Content;

public sealed class ConditionEvaluatorTests
{
    private static readonly AttributeRef Inflation =
        AttributeRef.From(TargetType.WorldVariable, "inflation", "current");

    private static ConditionEvaluator Evaluator(decimal inflation = 5m) =>
        new(new FakeWorld().WithValue(TargetType.WorldVariable, "inflation", "current", inflation));

    private static bool Evaluate(ConditionGroup group, decimal inflation = 5m) =>
        Evaluator(inflation).Evaluate(group, RoleBindings.Empty).Value;

    [Theory]
    [InlineData(ComparisonOperator.Equal, 5, true)]
    [InlineData(ComparisonOperator.Equal, 6, false)]
    [InlineData(ComparisonOperator.NotEqual, 6, true)]
    [InlineData(ComparisonOperator.GreaterThan, 4, true)]
    [InlineData(ComparisonOperator.GreaterThan, 5, false)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 5, true)]
    [InlineData(ComparisonOperator.LessThan, 6, true)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 5, true)]
    public void Every_comparison_operator_works(ComparisonOperator op, int value, bool expected)
    {
        var condition = Condition.Compare(Inflation, op, value).Value;

        Evaluate(ConditionGroup.All(condition)).ShouldBe(expected);
    }

    [Fact]
    public void A_range_is_inclusive_of_both_ends()
    {
        var condition = Condition.InRange(Inflation, 5m, 10m).Value;

        Evaluate(ConditionGroup.All(condition), inflation: 5m).ShouldBeTrue();
        Evaluate(ConditionGroup.All(condition), inflation: 10m).ShouldBeTrue();
        Evaluate(ConditionGroup.All(condition), inflation: 4.9m).ShouldBeFalse();
    }

    [Fact]
    public void An_inverted_range_is_rejected_at_construction()
    {
        Condition.InRange(Inflation, 10m, 5m).Error.Code.ShouldBe("condition.inverted_range");
    }

    [Fact]
    public void Exists_answers_without_a_value_being_readable()
    {
        var missing = AttributeRef.From(TargetType.WorldVariable, "nonexistent", "current");

        Evaluate(ConditionGroup.All(Condition.Exists(Inflation))).ShouldBeTrue();
        Evaluate(ConditionGroup.All(Condition.Exists(missing))).ShouldBeFalse();
        Evaluate(ConditionGroup.All(Condition.NotExists(missing))).ShouldBeTrue();
    }

    [Fact]
    public void All_requires_every_member()
    {
        var group = ConditionGroup.All(
            Condition.Compare(Inflation, ComparisonOperator.GreaterThan, 1m).Value,
            Condition.Compare(Inflation, ComparisonOperator.LessThan, 10m).Value);

        Evaluate(group).ShouldBeTrue();

        Evaluate(ConditionGroup.All(
            Condition.Compare(Inflation, ComparisonOperator.GreaterThan, 1m).Value,
            Condition.Compare(Inflation, ComparisonOperator.GreaterThan, 100m).Value)).ShouldBeFalse();
    }

    [Fact]
    public void Any_requires_only_one_member()
    {
        var group = ConditionGroup.Any(
            Condition.Compare(Inflation, ComparisonOperator.GreaterThan, 100m).Value,
            Condition.Compare(Inflation, ComparisonOperator.Equal, 5m).Value);

        Evaluate(group).ShouldBeTrue();
    }

    [Fact]
    public void Groups_nest()
    {
        var group = ConditionGroup.Nested(
            LogicalOperator.Any,
            ConditionGroup.All(Condition.Compare(Inflation, ComparisonOperator.GreaterThan, 100m).Value),
            ConditionGroup.All(Condition.Compare(Inflation, ComparisonOperator.Equal, 5m).Value));

        Evaluate(group).ShouldBeTrue();
    }

    [Fact]
    public void An_empty_group_holds_so_an_unconditioned_definition_is_always_eligible()
    {
        Evaluate(ConditionGroup.Always).ShouldBeTrue();
    }

    [Fact]
    public void An_unreadable_attribute_fails_rather_than_evaluating_true()
    {
        // v0.1 is explicit: an unsupported attribute must not silently evaluate as true.
        var missing = AttributeRef.From(TargetType.WorldVariable, "nonexistent", "current");
        var condition = Condition.Compare(missing, ComparisonOperator.GreaterThan, 1m).Value;

        var result = Evaluator().Evaluate(ConditionGroup.All(condition), RoleBindings.Empty);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_condition_reads_through_a_role_binding()
    {
        var world = new FakeWorld().WithValue(TargetType.Party, "party-a", "credibility", 42m);
        var evaluator = new ConditionEvaluator(world);

        var condition = Condition.Compare(
            AttributeRef.From(TargetType.Party, "$accused", "credibility"),
            ComparisonOperator.LessThan, 60m).Value;

        var bindings = RoleBindings.Empty.With(RoleName.From("$accused"), "party-a");

        evaluator.Evaluate(ConditionGroup.All(condition), bindings).Value.ShouldBeTrue();
    }

    [Fact]
    public void An_unbound_role_is_an_error_not_a_false()
    {
        var condition = Condition.Compare(
            AttributeRef.From(TargetType.Party, "$accused", "credibility"),
            ComparisonOperator.LessThan, 60m).Value;

        Evaluator().Evaluate(ConditionGroup.All(condition), RoleBindings.Empty).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Content_cannot_express_an_arbitrary_expression()
    {
        // The whole grammar is a target, an operator and a number. There is nowhere to put
        // a string that could be compiled or evaluated.
        typeof(Condition).GetProperties()
            .Select(p => p.PropertyType)
            .ShouldNotContain(typeof(string));
    }
}
