using Shouldly;
using TestResult = NetArchTest.Rules.TestResult;

namespace TheSpanishWay.Architecture.Tests;

/// <summary>
/// NetArchTest reports a failure as a list of type names. These helpers turn that into
/// an assertion whose message names the offenders, so a violation is actionable without
/// re-running under a debugger.
/// </summary>
internal static class ArchRuleAssertions
{
    internal static void ShouldBeSuccessful(this TestResult result, string because)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var offenders = result.FailingTypeNames ?? [];

        throw new ShouldAssertException(
            $"{because}{Environment.NewLine}Offending types:{Environment.NewLine}  " +
            string.Join($"{Environment.NewLine}  ", offenders.OrderBy(n => n, StringComparer.Ordinal)));
    }
}
