using TheSpanishWay.Domain.Simulation.ValueObjects;
using TheSpanishWay.Domain.World.Entities;
using TheSpanishWay.Domain.World.ValueObjects;

using TheSpanishWay.Domain.Shared.ValueObjects;

using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.World.Services;

/// <summary>
/// Drift of the world's indicators toward where they are heading.
/// <para>
/// This is what makes the world autonomous: it runs whether or not the player governs, and
/// whether or not the player does anything at all. Changes it produces are tagged
/// <c>External</c>, so government attribution can tell drift apart from policy.
/// </para>
/// </summary>
public static class VariableEvolution
{
    /// <summary>
    /// Advances the named indicators one cadence step. The caller decides when: the economy
    /// on weekly boundaries, society on monthly, demography on quarterly.
    /// </summary>
    public static IReadOnlyList<ClampDiagnostic> AdvanceOneStep(
        WorldState world,
        IReadOnlyCollection<VariableKey> keys)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(keys);

        var diagnostics = new List<ClampDiagnostic>();

        // Ordered so that two runs over the same state produce the same sequence of changes.
        foreach (var key in keys.OrderBy(k => k))
        {
            var lookup = world.Variable(key);

            if (lookup.IsFailure)
            {
                continue;
            }

            var variable = lookup.Value;
            var before = variable.BaseValue;
            var change = variable.EvolveOneStep();

            if (!change.WasTruncated)
            {
                continue;
            }

            diagnostics.Add(
                new ClampDiagnostic(
                    AttributeRef.From(
                        TargetType.WorldVariable, key.Value, "current"),
                    before + (change.Result.Value - before) + change.Truncated,
                    change.Result.Value,
                    change.Truncated,
                    Provenance.NaturalEvolution(key.Value)));
        }

        return diagnostics;
    }

    /// <summary>The economic indicators, advanced on a weekly boundary.</summary>
    public static IReadOnlyList<ClampDiagnostic> AdvanceEconomy(WorldState world) =>
        AdvanceOneStep(
            world,
            [
                WorldVariableCatalogue.Employment,
                WorldVariableCatalogue.Inflation,
                WorldVariableCatalogue.EconomicGrowth,
                WorldVariableCatalogue.PublicDebt,
                WorldVariableCatalogue.PublicDeficit,
            ]);

    /// <summary>The social indicators, advanced on a monthly boundary.</summary>
    public static IReadOnlyList<ClampDiagnostic> AdvanceSociety(WorldState world) =>
        AdvanceOneStep(
            world,
            [
                WorldVariableCatalogue.Education,
                WorldVariableCatalogue.Inequality,
                WorldVariableCatalogue.Security,
                WorldVariableCatalogue.Welfare,
                WorldVariableCatalogue.InstitutionalTrust,
            ]);
}
