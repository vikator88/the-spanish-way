namespace TheSpanishWay.Domain.Content.Enumerations;

/// <summary>
/// The comparisons content may express. A closed set on purpose: content describes what to
/// compare, never how, so no authored string is ever compiled or evaluated as code.
/// </summary>
public enum ComparisonOperator
{
    Equal = 0,
    NotEqual = 1,
    GreaterThan = 2,
    GreaterThanOrEqual = 3,
    LessThan = 4,
    LessThanOrEqual = 5,

    /// <summary>Inclusive of both ends.</summary>
    InRange = 6,

    /// <summary>The target resolves to something that exists in this game.</summary>
    Exists = 7,

    NotExists = 8,
}

/// <summary>How the members of a condition group combine.</summary>
public enum LogicalOperator
{
    /// <summary>Every member must hold.</summary>
    All = 0,

    /// <summary>At least one member must hold.</summary>
    Any = 1,
}
