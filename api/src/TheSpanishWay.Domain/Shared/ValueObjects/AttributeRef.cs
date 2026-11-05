using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Shared.Enumerations;

using TheSpanishWay.Domain.Shared.Services;

namespace TheSpanishWay.Domain.Shared.ValueObjects;

/// <summary>
/// Points at exactly one changeable attribute: which kind of thing, which instance, which
/// field. For example <c>(WorldVariable, "inflation", "current")</c> or
/// <c>(Party, "&lt;party id&gt;", "cohesion")</c>.
/// <para>
/// Effects address attributes only through this. They cannot name an arbitrary object path
/// and cannot execute anything, which is what keeps authored content from reaching into
/// parts of the model it has no business touching.
/// </para>
/// </summary>
public readonly record struct AttributeRef : IComparable<AttributeRef>
{
    private AttributeRef(TargetType target, string instance, string attribute)
    {
        Target = target;
        Instance = instance;
        Attribute = attribute;
    }

    public TargetType Target { get; }

    /// <summary>Which instance: a variable key, an identifier, or a composite for relations.</summary>
    public string Instance { get; }

    /// <summary>Which field of it, normalised to lower case.</summary>
    public string Attribute { get; }

    public static Result<AttributeRef> Create(TargetType target, string instance, string attribute)
    {
        if (string.IsNullOrWhiteSpace(instance))
        {
            return Result.Failure<AttributeRef>(
                new Error("attribute_ref.missing_instance", "An attribute reference needs an instance."));
        }

        if (string.IsNullOrWhiteSpace(attribute))
        {
            return Result.Failure<AttributeRef>(
                new Error("attribute_ref.missing_attribute", "An attribute reference needs an attribute key."));
        }

        return Result.Success(
            new AttributeRef(
                target,
                instance.Trim().ToLowerInvariant(),
                attribute.Trim().ToLowerInvariant()));
    }

    public static AttributeRef From(TargetType target, string instance, string attribute)
    {
        var result = Create(target, instance, attribute);

        return result.IsSuccess ? result.Value : throw new ArgumentException(result.Error.Message);
    }

    /// <summary>The attribute key alone, used to look a target up in the registry.</summary>
    public string RegistryKey => $"{Target}.{Attribute}";

    public int CompareTo(AttributeRef other)
    {
        var byTarget = Target.CompareTo(other.Target);

        if (byTarget != 0)
        {
            return byTarget;
        }

        var byInstance = string.CompareOrdinal(Instance, other.Instance);

        return byInstance != 0 ? byInstance : string.CompareOrdinal(Attribute, other.Attribute);
    }

    public static bool operator <(AttributeRef left, AttributeRef right) => left.CompareTo(right) < 0;

    public static bool operator <=(AttributeRef left, AttributeRef right) => left.CompareTo(right) <= 0;

    public static bool operator >(AttributeRef left, AttributeRef right) => left.CompareTo(right) > 0;

    public static bool operator >=(AttributeRef left, AttributeRef right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Target}:{Instance}.{Attribute}";
}
