using Shouldly;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;

namespace TheSpanishWay.Domain.UnitTests.Electorate;

public sealed class ElectoralSystemTests
{
    /// <summary>
    /// 50 provinces plus Ceuta and Melilla. Populations are evenly spread placeholders —
    /// the point is the distribution rule, not the demography.
    /// </summary>
    private static List<ConstituencyDefinition> SpanishConstituencies()
    {
        var constituencies = new List<ConstituencyDefinition>();

        for (var index = 0; index < 50; index++)
        {
            constituencies.Add(new ConstituencyDefinition(
                ProvinceId.From(Guid.Parse($"0000000d-0000-0000-0000-{index:D12}")),
                $"Province {index}",
                Population: 500_000L + (index * 100_000L),
                MinimumSeats: 2));
        }

        // Ceuta and Melilla return one seat each.
        constituencies.Add(new ConstituencyDefinition(
            ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000050")), "Ceuta", 85_000L, 1));
        constituencies.Add(new ConstituencyDefinition(
            ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000051")), "Melilla", 86_000L, 1));

        return constituencies;
    }

    [Fact]
    public void The_spanish_congress_has_350_seats_across_52_constituencies()
    {
        var system = ElectoralSystem.SpanishCongress(SpanishConstituencies()).Value;

        system.ChamberSize.ShouldBe(350);
        system.Constituencies.Count.ShouldBe(52);
        system.Constituencies.Sum(c => c.Seats).ShouldBe(350, "every seat is distributed, none lost");
    }

    [Fact]
    public void Every_province_gets_its_minimum()
    {
        var system = ElectoralSystem.SpanishCongress(SpanishConstituencies()).Value;

        foreach (var constituency in system.Constituencies)
        {
            constituency.Seats.ShouldBeGreaterThanOrEqualTo(constituency.MinimumSeats);
        }
    }

    [Fact]
    public void Ceuta_and_Melilla_return_a_single_seat_each()
    {
        var system = ElectoralSystem.SpanishCongress(SpanishConstituencies()).Value;

        var enclaves = system.Constituencies.Where(c => c.Name is "Ceuta" or "Melilla").ToList();

        enclaves.Count.ShouldBe(2);
        enclaves.ShouldAllBe(c => c.Seats == 1);
    }

    [Fact]
    public void More_populous_provinces_return_more_seats()
    {
        var system = ElectoralSystem.SpanishCongress(SpanishConstituencies()).Value;

        var ordered = system.Constituencies
            .Where(c => c.MinimumSeats == 2)
            .OrderBy(c => c.Population)
            .ToList();

        ordered[^1].Seats.ShouldBeGreaterThan(ordered[0].Seats);
    }

    [Fact]
    public void The_small_province_minimum_over_represents_them_on_purpose()
    {
        // Two seats for an eighth of the population of the largest province is the real
        // distortion in the Spanish system, and the model should reproduce it rather than
        // smooth it away.
        var system = ElectoralSystem.SpanishCongress(SpanishConstituencies()).Value;

        var smallest = system.Constituencies.Where(c => c.MinimumSeats == 2).MinBy(c => c.Population)!;
        var largest = system.Constituencies.Where(c => c.MinimumSeats == 2).MaxBy(c => c.Population)!;

        var smallestPerSeat = (decimal)smallest.Population / smallest.Seats;
        var largestPerSeat = (decimal)largest.Population / largest.Seats;

        smallestPerSeat.ShouldBeLessThan(largestPerSeat);
    }

    [Fact]
    public void An_absolute_majority_is_176()
    {
        ElectoralSystem.SpanishCongress(SpanishConstituencies()).Value.AbsoluteMajority.ShouldBe(176);
    }

    [Fact]
    public void The_threshold_is_three_percent()
    {
        ElectoralSystem.SpanishCongress(SpanishConstituencies()).Value
            .ThresholdOfValidVotes.ShouldBe(0.03m);
    }

    [Fact]
    public void Minimums_exceeding_the_chamber_are_rejected()
    {
        var oversized = Enumerable.Range(0, 10)
            .Select(i => new ConstituencyDefinition(
                ProvinceId.From(Guid.Parse($"0000000d-0000-0000-0000-{i:D12}")), $"P{i}", 1000L, 20))
            .ToList();

        ElectoralSystem.Create(50, 0.03m, oversized)
            .Error.Code.ShouldBe("electoral_system.minimums_exceed_chamber");
    }

    [Fact]
    public void Chamber_size_and_threshold_are_configuration_not_constants()
    {
        var small = ElectoralSystem.Create(
            100, 0.05m,
            [
                new ConstituencyDefinition(
                    ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000001")), "Only", 1000L, 1),
            ]).Value;

        small.ChamberSize.ShouldBe(100);
        small.AbsoluteMajority.ShouldBe(51);
        small.Constituencies[0].Seats.ShouldBe(100);
    }

    [Fact]
    public void A_party_stands_only_where_it_presents_a_candidacy()
    {
        var madrid = ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000001"));
        var bilbao = ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000002"));
        var national = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000a"));
        var regionalist = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000b"));

        var register = CandidacyRegister.Nationwide([madrid, bilbao], [national]);
        register.Stand(regionalist, bilbao);

        register.IsStanding(regionalist, bilbao).ShouldBeTrue();
        register.IsStanding(regionalist, madrid).ShouldBeFalse();
        register.StandingIn(madrid).ShouldBe([national]);
    }
}
