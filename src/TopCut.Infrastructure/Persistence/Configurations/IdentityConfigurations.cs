using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TopCut.Infrastructure.Identity;

namespace TopCut.Infrastructure.Persistence.Configurations;

/// <summary>
/// Only the columns TopCut adds to Identity. Everything else keeps the framework defaults, which
/// are fine and not worth diverging from.
/// </summary>
internal sealed class TopCutUserConfiguration : IEntityTypeConfiguration<TopCutUser>
{
    public void Configure(EntityTypeBuilder<TopCutUser> builder)
    {
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.ConsentVersion).HasMaxLength(32).IsRequired();

        // Resolving "which account organises for this store" has to be quick: it runs on the
        // Superadmin panel and whenever a store's permissions are re-checked.
        builder.HasIndex(user => user.StoreId);
    }
}
