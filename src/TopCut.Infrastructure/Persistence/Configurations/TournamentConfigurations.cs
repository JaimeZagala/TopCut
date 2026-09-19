using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TopCut.Domain.Catalog;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence.Converters;

namespace TopCut.Infrastructure.Persistence.Configurations;

internal sealed class TournamentConfiguration : EntityConfiguration<Tournament>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Tournament> builder)
    {
        builder.Property(tournament => tournament.Name).HasMaxLength(200).IsRequired();

        builder.Ignore(tournament => tournament.HasTopCut);
        builder.Ignore(tournament => tournament.SettingsLocked);
        builder.Ignore(tournament => tournament.Registered);
        builder.Ignore(tournament => tournament.Waitlist);
        builder.Ignore(tournament => tournament.ActiveParticipants);
        builder.Ignore(tournament => tournament.IsFull);
        builder.Ignore(tournament => tournament.CurrentRound);

        builder.OwnsOne(tournament => tournament.Venue, AddressConfiguration.Configure);
        builder.Navigation(tournament => tournament.Venue).IsRequired();

        builder.OwnsOne(tournament => tournament.Settings, settings =>
        {
            settings.Property(s => s.MinimumPercentage).HasPrecision(4, 3);
            settings.Property(s => s.RulesVersion).HasMaxLength(200).IsRequired();

            settings.Ignore(s => s.Tiebreakers);
            settings.Property<List<Tiebreaker>>("_tiebreakers")
                .HasColumnName("Tiebreakers")
                .HasMaxLength(64)
                .HasConversion(TiebreakerListConverter.Converter, TiebreakerListConverter.Comparer)
                .IsRequired();

            settings.OwnsOne(s => s.SwissPhase, PhaseSettingsConfiguration.Configure);
            settings.OwnsOne(s => s.TopCutPhase, PhaseSettingsConfiguration.Configure);
            settings.Navigation(s => s.SwissPhase).IsRequired();
            settings.Navigation(s => s.TopCutPhase).IsRequired();
        });
        builder.Navigation(tournament => tournament.Settings).IsRequired();

        builder.HasMany(tournament => tournament.Participants)
            .WithOne()
            .HasForeignKey(participant => participant.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(tournament => tournament.Participants)
            .HasField("_participants")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(tournament => tournament.Rounds)
            .WithOne()
            .HasForeignKey(round => round.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(tournament => tournament.Rounds)
            .HasField("_rounds")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Public search runs on TCG, date and status; the tenant filter narrows the rest.
        builder.HasIndex(tournament => new { tournament.Status, tournament.StartsAt });
        builder.HasIndex(tournament => new { tournament.TenantId, tournament.Status });
        builder.HasIndex(tournament => tournament.TcgId);
        builder.HasIndex(tournament => tournament.LeagueId);
    }
}

internal sealed class ParticipantConfiguration : EntityConfiguration<Participant>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Participant> builder)
    {
        builder.Property(participant => participant.Name).HasMaxLength(200).IsRequired();
        builder.Property(participant => participant.UserId).HasMaxLength(450);
        builder.Ignore(participant => participant.IsActive);

        // One account cannot hold two seats in the same tournament. The filtered index lets
        // walk-ins, who have no account at all, share the NULL.
        builder.HasIndex(participant => new { participant.TournamentId, participant.UserId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(participant => new { participant.TournamentId, participant.Status });
    }
}

internal sealed class RoundConfiguration : EntityConfiguration<Round>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Round> builder)
    {
        builder.Ignore(round => round.IsTimed);
        builder.Ignore(round => round.AllResultsReported);

        builder.HasMany(round => round.Pairings)
            .WithOne()
            .HasForeignKey(pairing => pairing.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(round => round.Pairings)
            .HasField("_pairings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(round => new { round.TournamentId, round.Number }).IsUnique();
    }
}

internal sealed class PairingConfiguration : EntityConfiguration<Pairing>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Pairing> builder)
    {
        builder.Ignore(pairing => pairing.IsBye);
        builder.Ignore(pairing => pairing.PlayerAWins);
        builder.Ignore(pairing => pairing.PlayerBWins);
        builder.Ignore(pairing => pairing.DrawnGames);

        builder.HasMany(pairing => pairing.Games)
            .WithOne()
            .HasForeignKey(game => game.PairingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(pairing => pairing.Games)
            .HasField("_games")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // No foreign key to Participant on purpose: deleting a participant must never cascade away
        // a match that already counts towards the standings.
        builder.HasIndex(pairing => new { pairing.RoundId, pairing.Table }).IsUnique();
        builder.HasIndex(pairing => pairing.PlayerAId);
        builder.HasIndex(pairing => pairing.PlayerBId);
    }
}

internal sealed class GameConfiguration : EntityConfiguration<Game>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Game> builder)
    {
        builder.HasIndex(game => new { game.PairingId, game.Number }).IsUnique();
    }
}
