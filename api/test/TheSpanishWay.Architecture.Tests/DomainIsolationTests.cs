using System.Text.RegularExpressions;
using NetArchTest.Rules;
using Shouldly;

namespace TheSpanishWay.Architecture.Tests;

/// <summary>
/// The Domain compiles against the BCL alone and uses neither ambient randomness
/// nor wall-clock time. See platform/solution-architecture, and domain/building-blocks
/// for the required replacements: IRandomSource and GameDate.
/// </summary>
public sealed class DomainIsolationTests
{
    /// <summary>Namespaces that would mean an infrastructure concern reached the Domain.</summary>
    private static readonly string[] BannedNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.DependencyInjection",
        "System.Text.Json",
        "Newtonsoft.Json",
    ];

    [Fact]
    public void Domain_project_declares_no_package_reference()
    {
        var csproj = ArchitectureReference.ProjectFile("TheSpanishWay.Domain").OpenText().ReadToEnd();

        csproj.ShouldNotContain(
            "<PackageReference",
            Case.Insensitive,
            "The Domain must compile against the BCL alone.");
    }

    [Fact]
    public void Domain_does_not_reference_banned_namespaces()
    {
        var result = Types.InAssembly(ArchitectureReference.Domain)
            .Should()
            .NotHaveDependencyOnAny(BannedNamespaces)
            .GetResult();

        result.ShouldBeSuccessful(
            "Persistence, HTTP, DI and serialisation concerns must not reach domain rules.");
    }

    [Fact]
    public void Domain_does_not_use_platform_randomness()
    {
        var offenders = FindInDomainSource(@"\bnew\s+Random\s*\(|\bRandom\.Shared\b|\bRandomNumberGenerator\b");

        offenders.ShouldBeEmpty(
            "Domain randomness must go through IRandomSource so draws stay reproducible.");
    }

    [Fact]
    public void Domain_does_not_use_wall_clock_time()
    {
        var offenders = FindInDomainSource(
            @"\bDateTime\.(Now|UtcNow|Today)\b|\bDateTimeOffset\.(Now|UtcNow)\b|\bTimeProvider\b");

        offenders.ShouldBeEmpty(
            "In-game time is GameDate; the Domain must never read a server clock.");
    }

    /// <summary>
    /// Matches against source rather than IL: a call to <c>DateTime.UtcNow</c> is a property
    /// access that NetArchTest's type-level dependency search does not reliably surface.
    /// </summary>
    private static List<string> FindInDomainSource(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in ArchitectureReference.SourceFiles("TheSpanishWay.Domain"))
        {
            var lines = File.ReadAllLines(file.FullName);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (regex.IsMatch(line))
                {
                    offenders.Add($"{file.Name}:{i + 1}: {line.Trim()}");
                }
            }
        }

        return offenders;
    }
}
