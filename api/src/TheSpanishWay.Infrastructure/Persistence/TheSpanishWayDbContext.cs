using Microsoft.EntityFrameworkCore;

namespace TheSpanishWay.Infrastructure.Persistence;

/// <summary>
/// The EF Core context.
/// <para>
/// Deliberately empty. Mappings, migrations and the physical schema are out of scope for the
/// bootstrap change; what matters now is that the domain model and the state-serialisation
/// boundary are settled, which they are. See <see cref="StorageStrategy"/> for the shape this
/// will take.
/// </para>
/// </summary>
public sealed class TheSpanishWayDbContext(DbContextOptions<TheSpanishWayDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        // Configurations arrive with the persistence slice.
    }
}
