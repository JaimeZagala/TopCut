using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TopCut.Domain.Tenancy;

namespace TopCut.Infrastructure.Persistence.Configurations;

internal sealed class StoreConfiguration : EntityConfiguration<Store>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Store> builder)
    {
        builder.Property(store => store.Name).HasMaxLength(200).IsRequired();
        builder.Ignore(store => store.CanOrganize);

        builder.OwnsOne(store => store.Address, AddressConfiguration.Configure);
        builder.Navigation(store => store.Address).IsRequired();

        // Public search filters by city, and test stores are excluded from it.
        builder.HasIndex(store => new { store.Status, store.IsTestStore });
    }
}

/// <summary>
/// The store's book of regulars (ADR-0035). No unique index on the name: two people in one shop can
/// genuinely be called the same thing, and the decision to fold two entries into one is the store's
/// to make from the directory screen, not the database's to force at the counter.
/// </summary>
internal sealed class StorePlayerConfiguration : EntityConfiguration<StorePlayer>
{
    protected override void ConfigureEntity(EntityTypeBuilder<StorePlayer> builder)
    {
        builder.Property(player => player.Name).HasMaxLength(200).IsRequired();
        builder.Property(player => player.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(player => player.UserId).HasMaxLength(450);
        builder.Ignore(player => player.IsMerged);

        // One entry per account per store. This one is worth enforcing: the account is the link
        // that cannot be wrong, and two rows for it would split somebody's league points in half.
        builder.HasIndex(player => new { player.TenantId, player.UserId })
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");

        // What the counter's autocomplete searches on.
        builder.HasIndex(player => new { player.TenantId, player.NormalizedName });
    }
}

/// <summary>
/// The address is flattened into the owner's row. Latitude and longitude are stored from v1 but
/// nothing reads them until the mobile phase adds "tournaments near me".
/// </summary>
internal static class AddressConfiguration
{
    public static void Configure<TOwner>(OwnedNavigationBuilder<TOwner, Address> builder)
        where TOwner : class
    {
        builder.Property(address => address.Street).HasMaxLength(200).IsRequired();
        builder.Property(address => address.City).HasMaxLength(100).IsRequired();
        builder.Property(address => address.Province).HasMaxLength(100).IsRequired();
        builder.Property(address => address.PostalCode).HasMaxLength(10).IsRequired();
        builder.Property(address => address.Latitude);
        builder.Property(address => address.Longitude);

        builder.HasIndex(address => address.City);
    }
}
