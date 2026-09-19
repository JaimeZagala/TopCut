using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TopCut.Domain.Leagues;

namespace TopCut.Infrastructure.Persistence.Configurations;

internal sealed class LeagueConfiguration : EntityConfiguration<League>
{
    protected override void ConfigureEntity(EntityTypeBuilder<League> builder)
    {
        builder.Property(league => league.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(league => new { league.TenantId, league.TcgId, league.IsOpen });
    }
}
