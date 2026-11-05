using Shouldly;
using TheSpanishWay.Domain.Electorate.Services;
using TheSpanishWay.Domain.Electorate.ValueObjects;
using TheSpanishWay.Domain.Shared.Identifiers;
using TheSpanishWay.TestKit;

namespace TheSpanishWay.Domain.UnitTests.Electorate;

public sealed class SeatAllocationTests
{
    private static readonly ProvinceId Province =
        ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000001"));

    private static readonly PartyId A = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000a"));
    private static readonly PartyId B = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000b"));
    private static readonly PartyId C = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000c"));
    private static readonly PartyId D = PartyId.From(Guid.Parse("0000000a-0000-0000-0000-00000000000d"));

    private static ProvincialResult Result(
        IReadOnlyDictionary<PartyId, long> votes, long blank = 0L, long electorate = 1_000_000L) =>
        new(Province, votes, blank, electorate);

    [Fact]
    public void The_textbook_dhondt_case_allocates_as_hand_calculated()
    {
        // A worked example checked by hand. 8 seats, votes A 100,000, B 80,000,
        // C 30,000, D 20,000.
        //
        // Quotients (votes ÷ 1, 2, 3, 4...):
        //   A: 100000, 50000, 33333, 25000
        //   B:  80000, 40000, 26667, 20000
        //   C:  30000, 15000
        //   D:  20000, 10000
        //
        // Eight highest: 100000(A) 80000(B) 50000(A) 40000(B) 33333(A) 30000(C)
        //                26667(B) 25000(A)
        // Therefore A=4, B=3, C=1, D=0.
        var allocation = SeatAllocationCalculator.Allocate(
            Result(new Dictionary<PartyId, long> { [A] = 100_000, [B] = 80_000, [C] = 30_000, [D] = 20_000 }),
            seats: 8,
            thresholdOfValidVotes: 0m,
            new ScriptedRandom());

        allocation.SeatsFor(A).ShouldBe(4);
        allocation.SeatsFor(B).ShouldBe(3);
        allocation.SeatsFor(C).ShouldBe(1);
        allocation.SeatsFor(D).ShouldBe(0);
        allocation.TotalSeats.ShouldBe(8);
    }

    [Fact]
    public void Dhondt_favours_the_largest_party()
    {
        // 50% of the vote taking more than 50% of the seats is the method working, not a bug.
        var allocation = SeatAllocationCalculator.Allocate(
            Result(new Dictionary<PartyId, long> { [A] = 50_000, [B] = 30_000, [C] = 20_000 }),
            seats: 5, thresholdOfValidVotes: 0m, new ScriptedRandom());

        allocation.SeatsFor(A).ShouldBe(3);
    }

    [Fact]
    public void The_threshold_is_a_share_of_valid_votes_including_blanks()
    {
        // C has 3,000 of 100,000 party votes, which would clear 3% on its own. With 10,000
        // blank ballots the valid total is 110,000 and the bar rises to 3,300, so C fails.
        var allocation = SeatAllocationCalculator.Allocate(
            Result(
                new Dictionary<PartyId, long> { [A] = 60_000, [B] = 37_000, [C] = 3_000 },
                blank: 10_000),
            seats: 10, thresholdOfValidVotes: 0.03m, new ScriptedRandom());

        allocation.ExcludedByThreshold.ShouldContain(C);
        allocation.SeatsFor(C).ShouldBe(0);
    }

    [Fact]
    public void A_party_just_above_the_threshold_qualifies()
    {
        var allocation = SeatAllocationCalculator.Allocate(
            Result(new Dictionary<PartyId, long> { [A] = 60_000, [B] = 36_000, [C] = 4_000 }),
            seats: 10, thresholdOfValidVotes: 0.03m, new ScriptedRandom());

        allocation.ExcludedByThreshold.ShouldNotContain(C);
    }

    [Fact]
    public void A_tie_on_quotient_goes_to_the_party_with_more_total_votes()
    {
        // Two seats, A on 60,000 and C on 30,000.
        //   Seat 1: A 60,000 ÷ 1 = 60,000 beats C 30,000 ÷ 1 = 30,000. A takes it.
        //   Seat 2: A 60,000 ÷ 2 = 30,000 ties C 30,000 ÷ 1 = 30,000 exactly.
        // The law breaks that tie on total votes, and A has twice as many — so A takes the
        // second seat too and C gets nothing, which is not what the quotients alone suggest.
        var allocation = SeatAllocationCalculator.Allocate(
            Result(new Dictionary<PartyId, long> { [A] = 60_000, [C] = 30_000 }),
            seats: 2, thresholdOfValidVotes: 0m, new ScriptedRandom());

        allocation.SeatsFor(A).ShouldBe(2);
        allocation.SeatsFor(C).ShouldBe(0);
    }

    [Fact]
    public void A_genuine_tie_is_drawn_from_the_seeded_stream()
    {
        // Equal votes and equal seats: indistinguishable, so the law draws lots.
        var votes = new Dictionary<PartyId, long> { [A] = 50_000, [B] = 50_000 };

        var firstDraw = SeatAllocationCalculator.Allocate(
            Result(votes), seats: 1, thresholdOfValidVotes: 0m,
            new ScriptedRandom().Script(Domain.Shared.Enumerations.RandomStream.Ties, 0.9m));

        var secondDraw = SeatAllocationCalculator.Allocate(
            Result(votes), seats: 1, thresholdOfValidVotes: 0m,
            new ScriptedRandom().Script(Domain.Shared.Enumerations.RandomStream.Ties, 0.1m));

        firstDraw.TotalSeats.ShouldBe(1);
        secondDraw.TotalSeats.ShouldBe(1);
        firstDraw.SeatsByParty.Keys.Where(p => firstDraw.SeatsFor(p) == 1)
            .ShouldNotBe(secondDraw.SeatsByParty.Keys.Where(p => secondDraw.SeatsFor(p) == 1));
    }

    [Fact]
    public void The_same_seed_resolves_a_tie_the_same_way()
    {
        var votes = new Dictionary<PartyId, long> { [A] = 50_000, [B] = 50_000 };

        var first = SeatAllocationCalculator.Allocate(
            Result(votes), 1, 0m, new ScriptedRandom().Script(Domain.Shared.Enumerations.RandomStream.Ties, 0.7m));
        var second = SeatAllocationCalculator.Allocate(
            Result(votes), 1, 0m, new ScriptedRandom().Script(Domain.Shared.Enumerations.RandomStream.Ties, 0.7m));

        first.SeatsFor(A).ShouldBe(second.SeatsFor(A));
    }

    [Fact]
    public void Allocation_is_pure_and_repeatable()
    {
        var votes = new Dictionary<PartyId, long> { [A] = 100_000, [B] = 80_000, [C] = 30_000 };

        var first = SeatAllocationCalculator.Allocate(Result(votes), 8, 0.03m, new ScriptedRandom());
        var second = SeatAllocationCalculator.Allocate(Result(votes), 8, 0.03m, new ScriptedRandom());

        first.SeatsByParty.ShouldBe(second.SeatsByParty);
    }

    [Fact]
    public void A_constituency_with_no_votes_returns_nothing()
    {
        SeatAllocationCalculator.Allocate(
            Result(new Dictionary<PartyId, long>()), 5, 0.03m, new ScriptedRandom())
            .TotalSeats.ShouldBe(0);
    }

    [Fact]
    public void National_seats_sum_across_constituencies()
    {
        var first = SeatAllocationCalculator.Allocate(
            Result(new Dictionary<PartyId, long> { [A] = 100_000, [B] = 50_000 }), 3, 0m, new ScriptedRandom());

        var otherProvince = new ProvincialResult(
            ProvinceId.From(Guid.Parse("0000000d-0000-0000-0000-000000000002")),
            new Dictionary<PartyId, long> { [A] = 20_000, [B] = 80_000 }, 0L, 200_000L);

        var second = SeatAllocationCalculator.Allocate(otherProvince, 3, 0m, new ScriptedRandom());

        var national = SeatAllocationCalculator.NationalSeats([first, second]);

        national[A].ShouldBe(first.SeatsFor(A) + second.SeatsFor(A));
        national.Values.Sum().ShouldBe(6);
    }
}
