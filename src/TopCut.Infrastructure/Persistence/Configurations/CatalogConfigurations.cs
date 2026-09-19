using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TopCut.Domain.Catalog;
using TopCut.Infrastructure.Persistence.Converters;

namespace TopCut.Infrastructure.Persistence.Configurations;

internal sealed class TcgConfiguration : IEntityTypeConfiguration<Tcg>
{
    public void Configure(EntityTypeBuilder<Tcg> builder)
    {
        builder.HasKey(tcg => tcg.Id);
        builder.Ignore(tcg => tcg.DomainEvents);

        builder.Property(tcg => tcg.Code).HasMaxLength(16).IsRequired();
        builder.Property(tcg => tcg.Name).HasMaxLength(100).IsRequired();

        // The code appears in URLs and search filters, so it has to stay unique.
        builder.HasIndex(tcg => tcg.Code).IsUnique();

        builder.HasOne(tcg => tcg.Ruleset)
            .WithOne(ruleset => ruleset.Tcg)
            .HasForeignKey<Ruleset>(ruleset => ruleset.TcgId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RulesetConfiguration : IEntityTypeConfiguration<Ruleset>
{
    public void Configure(EntityTypeBuilder<Ruleset> builder)
    {
        builder.HasKey(ruleset => ruleset.Id);
        builder.Ignore(ruleset => ruleset.DomainEvents);

        // Exposed as IReadOnlyList; the backing field is what actually gets stored.
        builder.Ignore(ruleset => ruleset.Tiebreakers);
        builder.Property<List<Tiebreaker>>("_tiebreakers")
            .HasColumnName("Tiebreakers")
            .HasMaxLength(64)
            .HasConversion(TiebreakerListConverter.Converter, TiebreakerListConverter.Comparer)
            .IsRequired();

        builder.Property(ruleset => ruleset.MinimumPercentage).HasPrecision(4, 3);
        builder.Property(ruleset => ruleset.RulesVersion).HasMaxLength(200).IsRequired();
        builder.Property(ruleset => ruleset.OfficialSource).HasMaxLength(500).IsRequired();

        builder.OwnsOne(ruleset => ruleset.SwissPhase, PhaseSettingsConfiguration.Configure);
        builder.OwnsOne(ruleset => ruleset.TopCutPhase, PhaseSettingsConfiguration.Configure);

        builder.Navigation(ruleset => ruleset.SwissPhase).IsRequired();
        builder.Navigation(ruleset => ruleset.TopCutPhase).IsRequired();

        builder.HasIndex(ruleset => ruleset.TcgId).IsUnique();
    }
}

/// <summary>
/// Phase settings live in the owner's row as three flattened columns. They are never queried on
/// their own, so a separate table would only add a join.
/// </summary>
internal static class PhaseSettingsConfiguration
{
    public static void Configure<TOwner>(OwnedNavigationBuilder<TOwner, PhaseSettings> builder)
        where TOwner : class
    {
        builder.Property(settings => settings.Format).IsRequired();
        builder.Property(settings => settings.RoundLength);
        builder.Property(settings => settings.ExtraTime).IsRequired();
        builder.Ignore(settings => settings.TotalLength);
    }
}
