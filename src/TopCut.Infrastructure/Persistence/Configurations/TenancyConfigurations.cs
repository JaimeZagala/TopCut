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
