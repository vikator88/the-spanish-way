using Shouldly;
using TheSpanishWay.Domain.Content.Enumerations;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Infrastructure.Content;

namespace TheSpanishWay.Infrastructure.IntegrationTests;

/// <summary>
/// Content is read from JSON and checked against the attribute registry, so a typo fails at
/// load rather than doing nothing at runtime.
/// </summary>
public sealed class JsonScenarioLoaderTests
{
    private static JsonScenarioLoader Loader() => new(new ContentValidator(AttributeRegistry.Default()));

    /// <summary>The scenario file that ships with the repository.</summary>
    private static string RealScenario()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && directory.Name != "api")
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(directory!.FullName, "content", "scenario-spain-2027.json"));
    }

    [Fact]
    public void The_shipped_scenario_loads_and_validates()
    {
        var loaded = Loader().Load(RealScenario());

        loaded.IsSuccess.ShouldBeTrue();
        loaded.Value.Violations.ShouldBeEmpty();
        loaded.Value.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Issues_load_as_positional_or_valence()
    {
        var scenario = Loader().Load(RealScenario()).Value;

        scenario.Issues.Single(i => i.Key == "pensions").Kind.ShouldBe(IssueKind.Positional);
        scenario.Issues.Single(i => i.Key == "corruption").Kind.ShouldBe(IssueKind.Valence);
        scenario.Issues.Single(i => i.Key == "corruption").AxisLoadings.ShouldBeEmpty();
    }

    [Fact]
    public void A_scandal_loads_with_its_roles_and_their_selection_rules()
    {
        var scandal = Loader().Load(RealScenario()).Value.Events.Single(e => e.Key == "corruption-scandal");

        scandal.Roles.Count.ShouldBe(2);
        scandal.Roles.ShouldContain(r => r.Name.Value == "$accused");
        scandal.Roles.Single(r => r.Name.Value == "$figure").ScopedTo!.Value.Value.ShouldBe("$accused");
        scandal.IsBlocking.ShouldBeTrue();
        scandal.CooldownDays.ShouldBe(180);
    }

    [Fact]
    public void A_value_range_survives_the_round_trip()
    {
        var scandal = Loader().Load(RealScenario()).Value.Events.Single(e => e.Key == "corruption-scandal");

        var credibilityEffect = scandal.Effects[0];

        credibilityEffect.Value.ShouldBeOfType<Domain.Content.ValueObjects.ValueExpression.Range>();
    }

    [Fact]
    public void Loading_is_deterministic_so_ids_do_not_change_between_runs()
    {
        var first = Loader().Load(RealScenario()).Value;
        var second = Loader().Load(RealScenario()).Value;

        first.Events.Select(e => e.Id).ShouldBe(second.Events.Select(e => e.Id));
        first.Issues.Select(i => i.Id).ShouldBe(second.Issues.Select(i => i.Id));
    }

    [Fact]
    public void An_unknown_attribute_is_reported_rather_than_ignored()
    {
        var json = """
        {
          "key": "broken", "name": "Broken",
          "events": [{
            "key": "typo", "category": "Random", "baseProbability": 0.1,
            "effects": [{
              "target": "WorldVariable", "instance": "inflation",
              "attribute": "currrent", "operation": "Add", "value": 1
            }]
          }]
        }
        """;

        var loaded = Loader().Load(json);

        loaded.Value.IsValid.ShouldBeFalse();
        loaded.Value.Violations.ShouldContain(v => v.Code == "attribute_registry.unknown_attribute");
    }

    [Fact]
    public void Content_writing_a_perception_is_refused()
    {
        var json = """
        {
          "key": "cheating", "name": "Cheating",
          "events": [{
            "key": "hand-placed-votes", "category": "Random", "baseProbability": 0.1,
            "effects": [{
              "target": "SegmentPerception", "instance": "$player",
              "attribute": "trust", "operation": "Add", "value": -10
            }]
          }]
        }
        """;

        Loader().Load(json).Value.Violations
            .ShouldContain(v => v.Code == "attribute_registry.computed_attribute");
    }

    [Fact]
    public void Malformed_json_fails_with_a_clear_error()
    {
        Loader().Load("{ this is not json").Error.Code.ShouldBe("content.malformed_json");
    }

    [Fact]
    public void Comments_and_trailing_commas_are_tolerated_in_authored_files()
    {
        var json = """
        {
          // Authors leave notes.
          "key": "fine", "name": "Fine",
          "issues": [],
        }
        """;

        Loader().Load(json).IsSuccess.ShouldBeTrue();
    }
}
