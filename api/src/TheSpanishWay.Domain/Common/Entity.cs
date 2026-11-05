namespace TheSpanishWay.Domain.Common;

/// <summary>
/// Something with a lifecycle and an identity that outlives its attribute values.
/// Equality is identity, never structure: a party whose cohesion changed is still the
/// same party. Contrast <see cref="ValueObject"/>, which is equal when its parts are.
/// </summary>
/// <typeparam name="TId">A strongly-typed identifier. See <c>Ids.cs</c>.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct, IEquatable<TId>
{
    protected Entity(TId id) => Id = id;

    /// <summary>Fixed at construction: an entity cannot change which thing it is.</summary>
    public TId Id { get; }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Two different kinds of thing are never equal, even sharing an underlying id value.
        return GetType() == other.GetType() && Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
