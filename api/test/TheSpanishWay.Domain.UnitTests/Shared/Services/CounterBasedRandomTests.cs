using Shouldly;
using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.Domain.UnitTests.Shared.Services;

/// <summary>
/// The determinism guarantee in one place. If these fail, saved games stop reproducing
/// and every replay-based test becomes unreliable.
/// </summary>
public sealed class CounterBasedRandomTests
{
    private static readonly RandomSeed Seed = RandomSeed.FromText("the-spanish-way");
    private static readonly GameDate Start = GameDate.From(2027, 3, 1);

    private static CounterBasedRandomSource NewSource() =>
        new(Seed, Start, RandomStreamIndices.Empty());

    [Fact]
    public void The_same_coordinates_always_produce_the_same_draw()
    {
        // Task 1.12: the cross-process guarantee. A pure function of its coordinates has
        // no process-local state to differ on, so equality here is equality anywhere.
        var first = CounterBasedRandom.Draw(Seed, RandomStream.Events, Start.DayNumber, 0);
        var second = CounterBasedRandom.Draw(Seed, RandomStream.Events, Start.DayNumber, 0);

        second.ShouldBe(first);
    }

    [Fact]
    public void Draws_are_pinned_to_known_values()
    {
        // Golden values. Changing the mixing function changes every game ever saved, so
        // that must be a deliberate act that breaks this test, not a silent refactor.
        CounterBasedRandom.Draw(RandomSeed.From(0), RandomStream.Events, 0, 0)
            .ShouldBe(10709605192711416551UL);
        CounterBasedRandom.Draw(RandomSeed.From(1), RandomStream.Events, 0, 0)
            .ShouldBe(9488101898964762787UL);
        CounterBasedRandom.Draw(RandomSeed.FromText("the-spanish-way"), RandomStream.Ties, 1000, 7)
            .ShouldBe(9809032188772181819UL);
    }

    [Theory]
    [InlineData(RandomStream.Events)]
    [InlineData(RandomStream.Bindings)]
    [InlineData(RandomStream.Media)]
    [InlineData(RandomStream.Reactions)]
    [InlineData(RandomStream.Actors)]
    [InlineData(RandomStream.Polls)]
    [InlineData(RandomStream.Ties)]
    public void Every_named_stream_exists(RandomStream stream)
    {
        RandomStreamIndices.Empty().IndexOf(stream).ShouldBe(0);
    }

    [Fact]
    public void Changing_any_coordinate_changes_the_draw()
    {
        var baseline = CounterBasedRandom.Draw(Seed, RandomStream.Events, 100, 0);

        CounterBasedRandom.Draw(Seed, RandomStream.Media, 100, 0).ShouldNotBe(baseline);
        CounterBasedRandom.Draw(Seed, RandomStream.Events, 101, 0).ShouldNotBe(baseline);
        CounterBasedRandom.Draw(Seed, RandomStream.Events, 100, 1).ShouldNotBe(baseline);
        CounterBasedRandom.Draw(RandomSeed.From(Seed.Value + 1), RandomStream.Events, 100, 0)
            .ShouldNotBe(baseline);
    }

    [Fact]
    public void An_extra_draw_on_one_stream_leaves_the_others_untouched()
    {
        // Task 1.13. This is why streams exist: adding a draw to the media model must not
        // shift which events fire, or every content tweak would rewrite the whole game.
        var undisturbed = NewSource();
        var expectedEvents = Enumerable.Range(0, 5)
            .Select(_ => undisturbed.NextInt(RandomStream.Events, 0, 1000))
            .ToList();

        var disturbed = NewSource();
        var actualEvents = new List<int>();

        for (var i = 0; i < 5; i++)
        {
            // Interleave extra media draws, as a new media rule would.
            disturbed.NextUnitInterval(RandomStream.Media);
            disturbed.NextUnitInterval(RandomStream.Media);
            actualEvents.Add(disturbed.NextInt(RandomStream.Events, 0, 1000));
        }

        actualEvents.ShouldBe(expectedEvents);
    }

    [Fact]
    public void Successive_draws_on_a_stream_differ()
    {
        var source = NewSource();

        var draws = Enumerable.Range(0, 20)
            .Select(_ => source.NextInt(RandomStream.Events, 0, int.MaxValue))
            .ToList();

        draws.Distinct().Count().ShouldBe(20);
    }

    [Fact]
    public void Indices_advance_once_per_draw()
    {
        var source = NewSource();

        source.NextUnitInterval(RandomStream.Polls);
        source.NextUnitInterval(RandomStream.Polls);

        source.Indices.IndexOf(RandomStream.Polls).ShouldBe(2);
        source.Indices.IndexOf(RandomStream.Events).ShouldBe(0);
    }

    [Fact]
    public void A_new_day_restarts_every_index()
    {
        var source = NewSource();
        source.NextUnitInterval(RandomStream.Events);

        source.AdvanceTo(Start.AddDays(1));

        source.Indices.IndexOf(RandomStream.Events).ShouldBe(0);
    }

    [Fact]
    public void Time_cannot_run_backwards()
    {
        var source = NewSource();

        Should.Throw<ArgumentException>(() => source.AdvanceTo(Start.AddDays(-1)));
    }

    [Fact]
    public void Resuming_from_a_persisted_index_continues_the_same_sequence()
    {
        var original = NewSource();
        original.NextInt(RandomStream.Events, 0, 1000);
        original.NextInt(RandomStream.Events, 0, 1000);
        var expected = original.NextInt(RandomStream.Events, 0, 1000);

        // Only the seed and the per-stream indices are persisted; nothing else is needed.
        var resumed = new CounterBasedRandomSource(
            Seed, Start, RandomStreamIndices.Restore(original.Indices.ToSnapshot()));

        // Rewind the snapshot to just before the third draw.
        var replay = new CounterBasedRandomSource(
            Seed,
            Start,
            RandomStreamIndices.Restore(new Dictionary<RandomStream, int> { [RandomStream.Events] = 2 }));

        replay.NextInt(RandomStream.Events, 0, 1000).ShouldBe(expected);
        resumed.Indices.IndexOf(RandomStream.Events).ShouldBe(3);
    }

    [Fact]
    public void Integer_draws_stay_inside_their_range()
    {
        var source = NewSource();

        for (var i = 0; i < 500; i++)
        {
            source.NextInt(RandomStream.Reactions, 10, 20).ShouldBeInRange(10, 19);
        }
    }

    [Fact]
    public void Unit_interval_draws_stay_in_zero_to_one()
    {
        var source = NewSource();

        for (var i = 0; i < 500; i++)
        {
            var value = source.NextUnitInterval(RandomStream.Reactions);

            value.ShouldBeGreaterThanOrEqualTo(0m);
            value.ShouldBeLessThan(1m);
        }
    }

    [Fact]
    public void Certain_and_impossible_chances_still_consume_a_draw()
    {
        // Otherwise tuning a probability to 0 or 1 would shift every later draw on
        // that stream, which is exactly the coupling streams exist to prevent.
        var source = NewSource();

        source.Chance(RandomStream.Events, 0m).ShouldBeFalse();
        source.Chance(RandomStream.Events, 1m).ShouldBeTrue();

        source.Indices.IndexOf(RandomStream.Events).ShouldBe(2);
    }

    [Fact]
    public void Chance_is_roughly_calibrated()
    {
        var source = NewSource();
        var hits = 0;

        for (var i = 0; i < 2000; i++)
        {
            if (source.Chance(RandomStream.Reactions, 0.25m))
            {
                hits++;
            }
        }

        hits.ShouldBeInRange(420, 580);
    }

    [Fact]
    public void Picking_from_an_empty_set_is_rejected()
    {
        Should.Throw<ArgumentException>(() => NewSource().Pick(RandomStream.Bindings, Array.Empty<string>()));
    }

    [Fact]
    public void Picking_returns_a_member_of_the_set()
    {
        var candidates = new[] { "a", "b", "c" };

        NewSource().Pick(RandomStream.Bindings, candidates).ShouldBeOneOf(candidates);
    }

    [Fact]
    public void Platform_randomness_is_not_used()
    {
        typeof(CounterBasedRandomSource).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ShouldNotContain("System.Security.Cryptography");
    }
}
