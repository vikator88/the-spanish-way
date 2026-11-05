using System.Reflection;
using System.Runtime.CompilerServices;
using Shouldly;
using TheSpanishWay.Domain.Common;

namespace TheSpanishWay.Architecture.Tests;

/// <summary>
/// Keeps the Domain's two foundation folders honest.
/// <para>
/// <c>Common</c> holds the DDD tactical patterns and nothing else — they would read the
/// same in any domain. <c>Shared</c> holds this game's shared kernel, classified by what
/// each type <em>is</em>: value object, identifier, enumeration, service.
/// </para>
/// </summary>
public sealed class DomainStructureTests
{
    private const string CommonNamespace = "TheSpanishWay.Domain.Common";
    private const string SharedNamespace = "TheSpanishWay.Domain.Shared";

    /// <summary>The DDD building blocks. Anything else appearing in Common is game vocabulary in the wrong place.</summary>
    private static readonly string[] PermittedCommonTypes =
    [
        "Entity`1",
        "AggregateRoot`1",
        "ValueObject",
        "IDomainEvent",
        "Result",
        "Result`1",
        "Error",
    ];

    /// <summary>
    /// The kinds a domain type can be. Anything that fits none of these needs a new folder
    /// and a deliberate decision about what it is, rather than a default home.
    /// </summary>
    private static readonly string[] ClassifiedFolders =
    [
        "Aggregates", "Entities", "ValueObjects", "Identifiers", "Enumerations", "Events", "Services",
        "Projections",
    ];

    [Fact]
    public void Common_holds_only_DDD_building_blocks()
    {
        var unexpected = TypesIn(CommonNamespace)
            .Select(t => t.Name)
            .Except(PermittedCommonTypes, StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        unexpected.ShouldBeEmpty(
            "Common is for patterns that would read the same in any domain. "
            + "Game vocabulary belongs in Shared, classified by kind.");
    }

    [Fact]
    public void Every_DDD_building_block_is_present()
    {
        var declared = TypesIn(CommonNamespace).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in PermittedCommonTypes)
        {
            declared.ShouldContain(expected);
        }
    }

    [Fact]
    public void Identifiers_are_comparable_value_types_named_Id()
    {
        var identifiers = TypesIn($"{SharedNamespace}.Identifiers").ToList();

        identifiers.ShouldNotBeEmpty();

        foreach (var type in identifiers)
        {
            type.Name.ShouldEndWith("Id", Case.Sensitive);
            type.IsValueType.ShouldBeTrue($"{type.Name} is an identifier and must be a struct");

            type.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IComparable<>))
                .ShouldBeTrue($"{type.Name} must order totally so iteration can be stable");
        }
    }

    [Fact]
    public void Enumerations_folder_holds_only_enums()
    {
        var types = TypesIn($"{SharedNamespace}.Enumerations").ToList();

        types.ShouldNotBeEmpty();

        foreach (var type in types)
        {
            // A static extension companion beside its enum is part of that enum's definition,
            // so it belongs here rather than being exiled to a Services folder.
            var isEnumOrCompanion = type.IsEnum || (type.IsAbstract && type.IsSealed);

            isEnumOrCompanion.ShouldBeTrue(
                $"{type.Name} is neither an enum nor a static companion to one.");
        }
    }

    [Fact]
    public void ValueObjects_folder_holds_only_value_objects()
    {
        foreach (var type in TypesIn($"{SharedNamespace}.ValueObjects"))
        {
            var isValueObject =
                typeof(ValueObject).IsAssignableFrom(type)
                || (type.IsValueType && IsRecord(type))
                || type.IsAbstract && type.IsSealed; // static companion, e.g. Bounded

            isValueObject.ShouldBeTrue(
                $"{type.Name} sits in ValueObjects but is neither a ValueObject nor a record struct.");
        }
    }

    [Fact]
    public void Shared_services_hold_no_entities()
    {
        foreach (var type in TypesIn($"{SharedNamespace}.Services"))
        {
            IsEntity(type).ShouldBeFalse($"{type.Name} is an entity and does not belong under Services.");
        }
    }

    [Fact]
    public void Shared_declares_no_type_outside_a_classified_folder()
    {
        var classified = ClassifiedFolders
            .Select(folder => $"{SharedNamespace}.{folder}")
            .ToArray();

        var unclassified = ArchitectureReference.Domain
            .GetTypes()
            .Where(IsDeclaredType)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith(SharedNamespace, StringComparison.Ordinal))
            .Where(t => !classified.Contains(t.Namespace, StringComparer.Ordinal))
            .Select(t => $"{t.Namespace}.{t.Name}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        unclassified.ShouldBeEmpty("every Shared type is classified by what it is.");
    }

    [Fact]
    public void Every_module_classifies_its_types_by_kind()
    {
        // The same rule as Shared, applied to every module: Common is the one exception,
        // because its contents are the classification vocabulary itself.
        const string domainRoot = "TheSpanishWay.Domain.";

        var unclassified = ArchitectureReference.Domain
            .GetTypes()
            .Where(IsDeclaredType)
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith(domainRoot, StringComparison.Ordinal))
            .Where(t => !string.Equals(t.Namespace, CommonNamespace, StringComparison.Ordinal))
            .Where(t => !ClassifiedFolders.Contains(
                t.Namespace![domainRoot.Length..].Split('.').Last(), StringComparer.Ordinal))
            .Select(t => $"{t.Namespace}.{t.Name}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        unclassified.ShouldBeEmpty(
            "every domain type outside Common sits in a folder naming what it is.");
    }

    [Fact]
    public void Domain_events_are_sealed_records_under_an_Events_folder()
    {
        var events = ArchitectureReference.Domain
            .GetTypes()
            .Where(IsDeclaredType)
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t != typeof(IDomainEvent))
            .ToList();

        events.ShouldNotBeEmpty();

        foreach (var type in events)
        {
            type.IsSealed.ShouldBeTrue($"{type.Name} is a domain event and must be sealed");
            IsRecord(type).ShouldBeTrue($"{type.Name} is a domain event and must be a record");
            type.Namespace.ShouldEndWith(".Events", Case.Sensitive);
        }
    }

    [Fact]
    public void The_full_state_query_is_reachable_only_from_the_operator_namespace()
    {
        // Imperfect information is a design principle. Owning a game is exactly what the
        // player does, so the full-state query is authorised by role, not by ownership, and
        // lives where that separation is visible.
        var application = ArchitectureReference.Application;

        var stateQueries = application.GetTypes()
            .Where(IsDeclaredType)
            .Where(t => t.Name.Contains("GameState", StringComparison.Ordinal))
            .ToList();

        stateQueries.ShouldNotBeEmpty();

        foreach (var query in stateQueries)
        {
            (query.Namespace ?? string.Empty).ShouldContain(
                "Operations",
                Case.Sensitive,
                $"{query.Name} returns hidden state and must not sit beside the player-facing queries.");
        }
    }

    [Fact]
    public void No_player_facing_query_returns_the_snapshot()
    {
        var playerQueries = ArchitectureReference.Application.GetTypes()
            .Where(IsDeclaredType)
            .Where(t => t.Namespace?.Contains("Games.Queries", StringComparison.Ordinal) == true)
            .ToList();

        playerQueries.ShouldNotBeEmpty();

        foreach (var query in playerQueries)
        {
            var returns = query.GetInterfaces()
                .Where(i => i.IsGenericType && i.Name.StartsWith("IQuery", StringComparison.Ordinal))
                .SelectMany(i => i.GetGenericArguments())
                .Select(a => a.Name)
                .ToList();

            returns.ShouldNotContain("GameStateSnapshot");
            returns.ShouldNotContain("GameState");
        }
    }

    private static IEnumerable<Type> TypesIn(string @namespace) =>
        ArchitectureReference.Domain
            .GetTypes()
            .Where(IsDeclaredType)
            .Where(t => string.Equals(t.Namespace, @namespace, StringComparison.Ordinal));

    /// <summary>Excludes compiler-generated scaffolding, which is not part of the model.</summary>
    private static bool IsDeclaredType(Type type) =>
        !type.IsNested
        && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null
        && !type.Name.StartsWith('<');

    private static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$") is not null
        || type.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic) is not null;

    private static bool IsEntity(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }
}
