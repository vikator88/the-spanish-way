using NetArchTest.Rules;
using Shouldly;

namespace TheSpanishWay.Architecture.Tests;

/// <summary>
/// Enforces the inward dependency rule from platform/solution-architecture.
/// References point only inward: Api -> Infrastructure -> Application -> Domain.
/// </summary>
public sealed class LayeringTests
{
    [Fact]
    public void Domain_depends_on_no_other_project()
    {
        var result = Types.InAssembly(ArchitectureReference.Domain)
            .Should()
            .NotHaveDependencyOnAny(
                "TheSpanishWay.Application",
                "TheSpanishWay.Infrastructure",
                "TheSpanishWay.Api")
            .GetResult();

        result.ShouldBeSuccessful("The Domain sits at the centre and depends on no other project.");
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(ArchitectureReference.Application)
            .Should()
            .NotHaveDependencyOnAny("TheSpanishWay.Infrastructure", "TheSpanishWay.Api")
            .GetResult();

        result.ShouldBeSuccessful("References point inward: the Application must not know its callers.");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(ArchitectureReference.Infrastructure)
            .Should()
            .NotHaveDependencyOnAny("TheSpanishWay.Api")
            .GetResult();

        result.ShouldBeSuccessful("Nothing references the Api project; it is the composition root.");
    }

    [Fact]
    public void Domain_project_declares_no_project_reference()
    {
        var csproj = ArchitectureReference.ProjectFile("TheSpanishWay.Domain").OpenText().ReadToEnd();

        csproj.ShouldNotContain(
            "<ProjectReference",
            Case.Insensitive,
            "The Domain must not reference any other project.");
    }

    [Fact]
    public void Application_project_references_only_the_Domain()
    {
        var csproj = ArchitectureReference.ProjectFile("TheSpanishWay.Application").OpenText().ReadToEnd();

        csproj.ShouldNotContain("TheSpanishWay.Infrastructure", Case.Insensitive);
        csproj.ShouldNotContain("TheSpanishWay.Api", Case.Insensitive);
        csproj.ShouldContain("TheSpanishWay.Domain", Case.Insensitive);
    }

    [Fact]
    public void No_source_project_references_the_Api_project()
    {
        foreach (var project in new[]
                 {
                     "TheSpanishWay.Domain",
                     "TheSpanishWay.Application",
                     "TheSpanishWay.Infrastructure",
                 })
        {
            var csproj = ArchitectureReference.ProjectFile(project).OpenText().ReadToEnd();

            csproj.ShouldNotContain(
                "TheSpanishWay.Api.csproj",
                Case.Insensitive,
                $"{project} must not reference the Api project.");
        }
    }
}
