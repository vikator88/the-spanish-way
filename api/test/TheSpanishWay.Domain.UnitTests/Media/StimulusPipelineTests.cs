using Shouldly;
using TheSpanishWay.Domain.Media.Entities;
using TheSpanishWay.Domain.Media.Services;
using TheSpanishWay.Domain.Media.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.Services;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Media;

/// <summary>
/// The stage that keeps content from placing votes by hand.
/// </summary>
public sealed class StimulusPipelineTests
{
    private static readonly GameDate Today = GameDate.From(2027, 3, 1);

    private static MediaOutlet Outlet(
        string name = "El Diario",
        EditorialOrientation orientation = EditorialOrientation.Centre,
        decimal audience = 40m,
        decimal credibility = 70m,
        decimal sensationalism = 40m,
        decimal interest = 0.9m) =>
        MediaOutlet.Create(
            MediaOutletId.From(Guid.Parse($"0000000e-0000-0000-0000-{Math.Abs(name.GetHashCode(StringComparison.Ordinal)) % 1000000:D12}")),
            name, orientation, audience, credibility, sensationalism,
            issueInterest: new Dictionary<IssueId, decimal> { [ElectorateFixtures.Pensions] = interest, [ElectorateFixtures.Corruption] = interest })
            .Value;

    private static Stimulus PensionsPledge(decimal stance = -70m, decimal intensity = 0.7m) =>
        Stimulus.Positional(
            PoliticsFixtures.PartyA, ElectorateFixtures.Pensions, stance, intensity).Value;

    private static MediaCoverage FullCoverage(MediaTone tone = MediaTone.Neutral) =>
        new(Outlet().Id, PoliticsFixtures.PartyA, ElectorateFixtures.Pensions,
            CoverageLevel.Prominent, tone, 0.5m, Today);

    [Fact]
    public void One_stimulus_moves_six_segments_by_six_different_amounts()
    {
        // The headline claim of the whole design. One authored line — "a hard pro-pension
        // stance, intensity 0.7" — and six segments react differently, with no author
        // writing six numbers.
        var stimulus = PensionsPledge();
        var coverage = FullCoverage();

        var deltas = ElectorateFixtures.SixSegments()
            .Select(segment => ReactionCalculator.React(
                segment, stimulus, ElectorateFixtures.PensionsIssue, coverage,
                agendaRelevance: 0.6m, outletCredibility: 70m, new ScriptedRandom(0.5m)))
            .ToList();

        deltas.Count.ShouldBe(6);
        deltas.Select(d => d.TrustChange).Distinct().Count().ShouldBe(6);
    }

    [Fact]
    public void A_segment_that_agrees_gains_trust_and_one_that_disagrees_loses_it()
    {
        var leftwardPledge = PensionsPledge(stance: -80m);
        var coverage = FullCoverage();

        var sympathetic = ReactionCalculator.React(
            ElectorateFixtures.Segment("Working class", -70m), leftwardPledge,
            ElectorateFixtures.PensionsIssue, coverage, 0.6m, 70m, new ScriptedRandom(0.5m));

        var hostile = ReactionCalculator.React(
            ElectorateFixtures.Segment("Self-employed", 70m), leftwardPledge,
            ElectorateFixtures.PensionsIssue, coverage, 0.6m, 70m, new ScriptedRandom(0.5m));

        sympathetic.TrustChange.ShouldBeGreaterThan(0m);
        hostile.TrustChange.ShouldBeLessThan(0m);
    }

    [Fact]
    public void A_dominant_issue_produces_a_much_larger_reaction_than_an_ignored_one()
    {
        var segment = ElectorateFixtures.Segment("Retired people", -10m, issueWeight: 0.9m);
        var stimulus = PensionsPledge();
        var coverage = FullCoverage();

        var dominant = ReactionCalculator.React(
            segment, stimulus, ElectorateFixtures.PensionsIssue, coverage,
            agendaRelevance: 1m, outletCredibility: 70m, new ScriptedRandom(0.5m));

        var ignored = ReactionCalculator.React(
            segment, stimulus, ElectorateFixtures.PensionsIssue, coverage,
            agendaRelevance: 0m, outletCredibility: 70m, new ScriptedRandom(0.5m));

        Math.Abs(dominant.TrustChange).ShouldBeGreaterThan(Math.Abs(ignored.TrustChange));
    }

    [Fact]
    public void A_valence_stimulus_is_judged_on_blame_not_ideology()
    {
        // Nobody is pro-corruption, so two segments on opposite sides of the economy axis
        // should both react badly to a hostile scandal story.
        var scandal = Stimulus.Valence(
            PoliticsFixtures.PartyA, ElectorateFixtures.Corruption, 0.8m).Value;

        var hostileCoverage = new MediaCoverage(
            Outlet().Id, PoliticsFixtures.PartyA, ElectorateFixtures.Corruption,
            CoverageLevel.Saturation, MediaTone.Hostile, 0.6m, Today);

        var left = ReactionCalculator.React(
            ElectorateFixtures.Segment("Working class", -70m, issue: ElectorateFixtures.Corruption),
            scandal, ElectorateFixtures.CorruptionIssue, hostileCoverage, 0.8m, 70m, new ScriptedRandom(0.5m));

        var right = ReactionCalculator.React(
            ElectorateFixtures.Segment("Self-employed", 70m, issue: ElectorateFixtures.Corruption),
            scandal, ElectorateFixtures.CorruptionIssue, hostileCoverage, 0.8m, 70m, new ScriptedRandom(0.5m));

        left.TrustChange.ShouldBeLessThan(0m);
        right.TrustChange.ShouldBeLessThan(0m);
    }

    [Fact]
    public void Hostile_coverage_harms_however_wide_it_is()
    {
        // Exposure is not approval: more of a bad story is worse, not better.
        var segment = ElectorateFixtures.Segment("Retired people", -10m, issue: ElectorateFixtures.Corruption);
        var scandal = Stimulus.Valence(PoliticsFixtures.PartyA, ElectorateFixtures.Corruption, 0.8m).Value;

        var narrow = ReactionCalculator.React(
            segment, scandal, ElectorateFixtures.CorruptionIssue,
            new MediaCoverage(Outlet().Id, PoliticsFixtures.PartyA, ElectorateFixtures.Corruption,
                CoverageLevel.Passing, MediaTone.Hostile, 0.1m, Today),
            0.8m, 70m, new ScriptedRandom(0.5m));

        var saturating = ReactionCalculator.React(
            segment, scandal, ElectorateFixtures.CorruptionIssue,
            new MediaCoverage(Outlet().Id, PoliticsFixtures.PartyA, ElectorateFixtures.Corruption,
                CoverageLevel.Saturation, MediaTone.Hostile, 0.9m, Today),
            0.8m, 70m, new ScriptedRandom(0.5m));

        saturating.TrustChange.ShouldBeLessThan(narrow.TrustChange);
    }

    [Fact]
    public void A_more_media_sensitive_segment_moves_further_on_the_same_story()
    {
        var stimulus = PensionsPledge();
        var coverage = FullCoverage();

        var sensitive = ReactionCalculator.React(
            ElectorateFixtures.Segment("Young", -40m, mediaSensitivity: 0.9m),
            stimulus, ElectorateFixtures.PensionsIssue, coverage, 0.6m, 70m, new ScriptedRandom(0.5m));

        var indifferent = ReactionCalculator.React(
            ElectorateFixtures.Segment("Young", -40m, mediaSensitivity: 0.1m),
            stimulus, ElectorateFixtures.PensionsIssue, coverage, 0.6m, 70m, new ScriptedRandom(0.5m));

        Math.Abs(sensitive.TrustChange).ShouldBeGreaterThan(Math.Abs(indifferent.TrustChange));
    }

    [Fact]
    public void A_more_credible_outlet_moves_opinion_further()
    {
        var stimulus = PensionsPledge();
        var coverage = FullCoverage();
        var segment = ElectorateFixtures.Segment("Retired", -10m);

        var credible = ReactionCalculator.React(
            segment, stimulus, ElectorateFixtures.PensionsIssue, coverage, 0.6m,
            outletCredibility: 95m, new ScriptedRandom(0.5m));

        var tabloid = ReactionCalculator.React(
            segment, stimulus, ElectorateFixtures.PensionsIssue, coverage, 0.6m,
            outletCredibility: 20m, new ScriptedRandom(0.5m));

        Math.Abs(credible.TrustChange).ShouldBeGreaterThan(Math.Abs(tabloid.TrustChange));
    }

    [Fact]
    public void Internal_business_is_far_less_likely_to_be_covered()
    {
        var outlet = Outlet(sensationalism: 50m);

        var publicStimulus = Stimulus.Positional(
            PoliticsFixtures.PartyA, ElectorateFixtures.Pensions, -70m, 0.9m, Visibility.Public).Value;

        var internalStimulus = Stimulus.Positional(
            PoliticsFixtures.PartyA, ElectorateFixtures.Pensions, -70m, 0.9m, Visibility.Internal).Value;

        var publicPickUps = 0;
        var internalPickUps = 0;

        for (var seed = 0; seed < 60; seed++)
        {
            var random = new ScriptedRandom(seed / 60m);

            if (MediaCoverageCalculator.Evaluate(outlet, publicStimulus, 0.8m, Today, random).WasCovered)
            {
                publicPickUps++;
            }

            if (MediaCoverageCalculator.Evaluate(outlet, internalStimulus, 0.8m, Today, new ScriptedRandom(seed / 60m)).WasCovered)
            {
                internalPickUps++;
            }
        }

        publicPickUps.ShouldBeGreaterThan(internalPickUps);
    }

    [Fact]
    public void Different_outlets_reach_different_verdicts_on_the_same_story()
    {
        var friendly = MediaOutlet.Create(
            MediaOutletId.From(Guid.NewGuid()), "Friendly", EditorialOrientation.Left,
            partyAffinity: new Dictionary<PartyId, decimal> { [PoliticsFixtures.PartyA] = 90m },
            issueInterest: new Dictionary<IssueId, decimal> { [ElectorateFixtures.Pensions] = 1m }).Value;

        var hostile = MediaOutlet.Create(
            MediaOutletId.From(Guid.NewGuid()), "Hostile", EditorialOrientation.Right,
            partyAffinity: new Dictionary<PartyId, decimal> { [PoliticsFixtures.PartyA] = 10m },
            issueInterest: new Dictionary<IssueId, decimal> { [ElectorateFixtures.Pensions] = 1m }).Value;

        var stimulus = PensionsPledge();

        var friendlyCoverage = MediaCoverageCalculator.Evaluate(
            friendly, stimulus, 0.8m, Today, new ScriptedRandom(0.2m));
        var hostileCoverage = MediaCoverageCalculator.Evaluate(
            hostile, stimulus, 0.8m, Today, new ScriptedRandom(0.2m));

        friendlyCoverage.ToneFactor.ShouldBeGreaterThan(hostileCoverage.ToneFactor);
    }

    [Fact]
    public void An_outlet_remembers_how_it_has_been_framing_a_party()
    {
        var outlet = Outlet();

        outlet.RecentFramingBiasToward(PoliticsFixtures.PartyA).ShouldBe(0m);

        outlet.RememberFraming(PoliticsFixtures.PartyA, -1m);
        outlet.RememberFraming(PoliticsFixtures.PartyA, -1m);

        outlet.RecentFramingBiasToward(PoliticsFixtures.PartyA).ShouldBeLessThan(0m);

        outlet.DecayMemory(0.9m);

        outlet.RecentFramingBiasToward(PoliticsFixtures.PartyA).ShouldBeGreaterThan(-0.2m);
    }

    [Fact]
    public void Coverage_is_reproducible_from_the_same_seed()
    {
        var outlet = Outlet();
        var stimulus = PensionsPledge();

        var first = MediaCoverageCalculator.Evaluate(outlet, stimulus, 0.7m, Today, new ScriptedRandom(0.3m));
        var second = MediaCoverageCalculator.Evaluate(Outlet(), stimulus, 0.7m, Today, new ScriptedRandom(0.3m));

        first.Level.ShouldBe(second.Level);
        first.Tone.ShouldBe(second.Tone);
    }

    [Fact]
    public void The_reaction_pipeline_is_the_only_writer_of_perceptions()
    {
        // Content validation refuses the target; this asserts the other half, that only one
        // place in the Domain produces a perception change at all.
        var writers = typeof(ReactionCalculator).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsNested && t != typeof(PerceptionDelta))
            .SelectMany(t => t.GetMethods())
            .Where(m => m.ReturnType == typeof(PerceptionDelta))
            .Select(m => m.DeclaringType!.Name)
            .Distinct()
            .ToList();

        writers.ShouldBe([nameof(ReactionCalculator)]);
    }
}
