using System.Reflection;

namespace TheSpanishWay.Architecture.Tests;

/// <summary>
/// The four assemblies under test, plus the project-file locations the structural
/// rules inspect. Assemblies are pinned by a marker type so a rename cannot silently
/// turn a rule into a no-op.
/// </summary>
internal static class ArchitectureReference
{
    internal static Assembly Domain { get; } = LoadByName("TheSpanishWay.Domain");

    internal static Assembly Application { get; } = LoadByName("TheSpanishWay.Application");

    internal static Assembly Infrastructure { get; } = LoadByName("TheSpanishWay.Infrastructure");

    internal static Assembly Api { get; } = LoadByName("TheSpanishWay.Api");

    /// <summary>The <c>api/</c> directory, found by walking up from the test binaries.</summary>
    internal static DirectoryInfo ApiRoot { get; } = FindApiRoot();

    internal static FileInfo ProjectFile(string projectName)
    {
        var candidate = new FileInfo(
            Path.Combine(ApiRoot.FullName, "src", projectName, $"{projectName}.csproj"));

        if (!candidate.Exists)
        {
            throw new FileNotFoundException($"Could not locate {projectName}.csproj.", candidate.FullName);
        }

        return candidate;
    }

    internal static IReadOnlyList<FileInfo> SourceFiles(string projectName) =>
        new DirectoryInfo(Path.Combine(ApiRoot.FullName, "src", projectName))
            .GetFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(f => f.FullName, StringComparer.Ordinal)
            .ToList();

    private static Assembly LoadByName(string name) => Assembly.Load(new AssemblyName(name));

    private static DirectoryInfo FindApiRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && directory.Name != "api")
        {
            directory = directory.Parent;
        }

        return directory
            ?? throw new DirectoryNotFoundException("Could not locate the 'api' directory from the test output path.");
    }
}
