using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;
using TheSpanishWay.Domain.Simulation.Entities;
using TheSpanishWay.Domain.Simulation.Enumerations;
using TheSpanishWay.Domain.Simulation.Services;
using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.Domain.World.Entities;
using TheSpanishWay.Domain.World.Services;
using TheSpanishWay.Domain.World.Enumerations;
using TheSpanishWay.Domain.World.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.TestKit;

/// <summary>
/// A small world with an effect engine wired to it, so an effect test says only what it is
/// about. Defaults are fixed, never generated.
/// </summary>
public sealed class EffectScenario
{
    private EffectScenario(WorldState world, EffectContext context, EffectEngine engine)
    {
        World = world;
        Context = context;
        Engine = engine;
    }

    public WorldState World { get; }

    public EffectContext Context { get; private set; }

    public EffectEngine Engine { get; }

    public GameDate Today => Context.CurrentDate;

    public ModifierStore Modifiers => Context.Modifiers;

    public PendingEffectQueue Pending => Context.Pending;

    /// <summary>
    /// A world holding one variable. Defaults to the clamped-expiry case from the design:
    /// inflation at 9.5 with a ceiling of 10.
    /// </summary>
    public static EffectScenario WithVariable(
        string key = "inflation",
        decimal value = 9.5m,
        decimal minimum = 0m,
        decimal maximum = 10m,
        VariablePolarity polarity = VariablePolarity.HigherIsWorse,
        decimal? target = null,
        decimal evolutionRate = 0m)
    {
        var variable = WorldVariable
            .Create(VariableKey.From(key), value, minimum, maximum, polarity, target, evolutionRate)
            .Value;

        var world = WorldState.From([variable]);

        var context = new EffectContext(
            GameDate.From(2027, 3, 1),
            world,
            ModifierStore.Empty(),
            PendingEffectQueue.Empty(),
            new SequentialIdentifierSource(RandomSeed.FromText("effect-tests")));

        return new EffectScenario(world, context, new EffectEngine(AttributeRegistry.Default()));
    }

    public static EffectScenario WithFullWorld()
    {
        var world = WorldState.From(
            [.. WorldVariableCatalogue.Economy(), .. WorldVariableCatalogue.Society()]);

        var context = new EffectContext(
            GameDate.From(2027, 3, 1),
            world,
            ModifierStore.Empty(),
            PendingEffectQueue.Empty(),
            new SequentialIdentifierSource(RandomSeed.FromText("effect-tests")));

        return new EffectScenario(world, context, new EffectEngine(AttributeRegistry.Default()));
    }

    public static AttributeRef Current(string key = "inflation") =>
        AttributeRef.From(TargetType.WorldVariable, key, "current");

    public decimal BaseValue(string key = "inflation") =>
        World.Variable(VariableKey.From(key)).Value.BaseValue;

    /// <summary>What a rule actually reads: the base combined with every active modifier.</summary>
    public decimal EffectiveValue(string key = "inflation")
    {
        var variable = World.Variable(VariableKey.From(key)).Value;
        var bounds = Bounded.From(variable.BaseValue, variable.Minimum, variable.Maximum);

        return Domain.Simulation.ValueObjects.EffectiveValue.Compute(
            variable.BaseValue, bounds, Modifiers.For(Current(key)));
    }

    /// <summary>Moves the clock on, expiring whatever is due, as the daily loop does.</summary>
    public EffectScenario AdvanceTo(GameDate date)
    {
        Context = Context with { CurrentDate = date };
        Modifiers.ExpireAsOf(date);

        return this;
    }

    public EffectScenario AdvanceDays(int days) => AdvanceTo(Today.AddDays(days));

    public static Provenance SomeAction(string id = "action-1", ChangeOrigin origin = ChangeOrigin.Policy) =>
        new(ChangeSource.ActionExecution, id, 0, origin);
}
