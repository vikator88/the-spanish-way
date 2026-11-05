using TheSpanishWay.Domain.Common;
using TheSpanishWay.Domain.Content.Services;
using TheSpanishWay.Domain.Content.ValueObjects;
using TheSpanishWay.Domain.Shared.Enumerations;
using TheSpanishWay.Domain.Shared.Services;
using TheSpanishWay.Domain.Shared.ValueObjects;

namespace TheSpanishWay.TestKit;

/// <summary>
/// A hand-written world for condition and role tests: attribute values keyed by reference,
/// and candidate instances keyed by kind.
/// <para>
/// Parties and characters arrive in later slices, so the real sources do not exist yet. This
/// stands in for them without pretending to be them.
/// </para>
/// </summary>
public sealed class FakeWorld : IAttributeReader, IRoleCandidateSource
{
    private readonly Dictionary<AttributeRef, decimal> _values = [];
    private readonly Dictionary<TargetType, List<string>> _candidates = [];
    private readonly Dictionary<string, List<string>> _membership = [];
    private readonly Dictionary<RoleName, string> _builtIns = [];

    public FakeWorld WithValue(TargetType target, string instance, string attribute, decimal value)
    {
        _values[AttributeRef.From(target, instance, attribute)] = value;

        return this;
    }

    public FakeWorld WithCandidates(TargetType kind, params string[] instances)
    {
        _candidates[kind] = [.. instances];

        return this;
    }

    /// <summary>Members of a party, for scoped roles such as <c>$figure</c> within <c>$accused</c>.</summary>
    public FakeWorld WithMembers(string owner, params string[] members)
    {
        _membership[owner.ToLowerInvariant()] = [.. members];

        return this;
    }

    public FakeWorld WithBuiltIn(RoleName role, string instance)
    {
        _builtIns[role] = instance;

        return this;
    }

    public Result<decimal> Read(AttributeRef target) =>
        _values.TryGetValue(target, out var value)
            ? Result.Success(value)
            : Result.Failure<decimal>(new Error("fake.missing", $"No value for {target}."));

    public IReadOnlyList<string> Candidates(TargetType kind) =>
        _candidates.TryGetValue(kind, out var list)
            ? [.. list.OrderBy(i => i, StringComparer.Ordinal)]
            : [];

    public IReadOnlyList<string> CandidatesWithin(TargetType kind, string ownerInstance) =>
        _membership.TryGetValue(ownerInstance.ToLowerInvariant(), out var list)
            ? [.. list.OrderBy(i => i, StringComparer.Ordinal)]
            : [];

    public Result<string> ResolveBuiltIn(RoleName role) =>
        _builtIns.TryGetValue(role, out var instance)
            ? Result.Success(instance)
            : Result.Failure<string>(new Error("fake.no_builtin", $"No binding for {role}."));
}

/// <summary>
/// A random source that returns a prepared sequence per stream, so a test can say exactly
/// which event fires and which candidate is picked.
/// </summary>
public sealed class ScriptedRandom : IRandomSource
{
    private readonly Dictionary<RandomStream, Queue<decimal>> _scripted = [];
    private readonly decimal _fallback;

    public ScriptedRandom(decimal fallback = 0.5m) => _fallback = fallback;

    /// <summary>Queues unit-interval draws for a stream, consumed in order.</summary>
    public ScriptedRandom Script(RandomStream stream, params decimal[] draws)
    {
        _scripted[stream] = new Queue<decimal>(draws);

        return this;
    }

    public int NextInt(RandomStream stream, int minInclusive, int maxExclusive)
    {
        var scaled = minInclusive + (int)(Next(stream) * (maxExclusive - minInclusive));

        return Math.Clamp(scaled, minInclusive, maxExclusive - 1);
    }

    public decimal NextUnitInterval(RandomStream stream) => Next(stream);

    public bool Chance(RandomStream stream, decimal probability) => Next(stream) < probability;

    public T Pick<T>(RandomStream stream, IReadOnlyList<T> orderedCandidates)
    {
        ArgumentNullException.ThrowIfNull(orderedCandidates);

        return orderedCandidates[NextInt(stream, 0, orderedCandidates.Count)];
    }

    private decimal Next(RandomStream stream) =>
        _scripted.TryGetValue(stream, out var queue) && queue.Count > 0 ? queue.Dequeue() : _fallback;
}
