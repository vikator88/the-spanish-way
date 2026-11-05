namespace TheSpanishWay.Infrastructure.Persistence;

/// <summary>
/// How a game is stored. Documented here because the shape is a decision, and the mappings
/// that will implement it are deliberately not written yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>Relational header.</b> Game id, owner, name, status, phase, current date, revision,
/// schema version, simulation version, pinned scenario version. Queryable without touching
/// the snapshot, which is what a list of saved games needs.
/// </para>
/// <para>
/// <b>Current snapshot</b> as one <c>jsonb</c> column. The whole mutable state, written in
/// full on each command. Acceptable because <c>AdvanceUntil</c> persists once per player
/// interaction rather than once per simulated day — at per-day granularity this would be
/// 1,461 writes per legislature, which is why that decision is not optional.
/// </para>
/// <para>
/// <b>Monthly snapshots</b> for history charts and for replaying from a midpoint instead of
/// from the beginning.
/// </para>
/// <para>
/// <b>Append-only history</b>: elections, polls, action executions, event occurrences. Written
/// in the same transaction as the snapshot, never read back to compute the next state.
/// </para>
/// <para>
/// <b>Append-only command log</b>: type, parameters, client command id, resulting revision.
/// Unique on (game, client command id), which is what makes retries idempotent. With the seed
/// and the scenario version this replays the whole game.
/// </para>
/// <para>
/// <b>Not EF entities.</b> The domain model is not mapped class by class. The snapshot is
/// serialised as a document, so no navigation property or parameterless constructor exists
/// anywhere for the ORM's benefit.
/// </para>
/// </remarks>
public static class StorageStrategy
{
    /// <summary>The jsonb column holding the whole mutable state.</summary>
    public const string SnapshotColumn = "state";

    /// <summary>Unique with the game id, which is what gives idempotency.</summary>
    public const string ClientCommandIdColumn = "client_command_id";
}
