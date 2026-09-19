using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TopCut.Domain.Common;

namespace TopCut.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared plumbing for every business entity: the Guid v7 key is assigned by the domain rather
/// than the database, domain events are never persisted, and anything implementing
/// <see cref="IHasRowVersion"/> gets its optimistic concurrency token wired up.
/// </summary>
internal abstract class EntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BusinessEntity
{
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);

        // Guid.CreateVersion7() already produces a sequential key, so the database must not
        // overwrite it (ADR-0006).
        builder.Property(entity => entity.Id).ValueGeneratedNever();

        builder.Ignore(entity => entity.DomainEvents);

        if (typeof(IHasRowVersion).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property(nameof(IHasRowVersion.RowVersion)).IsRowVersion();
        }

        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}
