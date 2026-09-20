using Microsoft.EntityFrameworkCore;
using TopCut.Domain.Catalog;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
public class CatalogPersistenceTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task The_seeder_creates_both_TCGs_with_their_rulesets()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);

        var tcgs = await db.Tcgs.Include(t => t.Ruleset).OrderBy(t => t.Code).ToListAsync();

        Assert.Equal([SeedCatalog.MagicCode, SeedCatalog.OnePieceCode], tcgs.Select(t => t.Code));
        Assert.All(tcgs, tcg => Assert.NotNull(tcg.Ruleset));
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_the_catalog()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        await CatalogSeeder.SeedAsync(db);

        Assert.Equal(1, await db.Tcgs.CountAsync(t => t.Code == SeedCatalog.MagicCode));
        Assert.Equal(2, await db.Rulesets.CountAsync());
        Assert.Equal(2, await db.Tcgs.CountAsync());
    }

    [Fact]
    public async Task The_Magic_ruleset_survives_a_round_trip_intact()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        db.ChangeTracker.Clear();

        var magic = await db.Rulesets.SingleAsync(r => r.Tcg.Code == SeedCatalog.MagicCode);

        Assert.Equal(3, magic.WinPoints);
        Assert.Equal(MatchFormat.Bo3, magic.SwissPhase.Format);
        Assert.Equal(TimeSpan.FromMinutes(50), magic.SwissPhase.RoundLength);
        Assert.Equal(TimeSpan.FromMinutes(90), magic.TopCutPhase.RoundLength);
        Assert.Equal(0.33m, magic.MinimumPercentage);
        Assert.True(magic.ByesCountInOwnWinPercentage);
        Assert.Equal(TimeExpiryPolicy.LeaderWins, magic.TimeExpiry);
    }

    [Fact]
    public async Task The_tiebreaker_order_survives_a_round_trip()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        db.ChangeTracker.Clear();

        var magic = await db.Rulesets.SingleAsync(r => r.Tcg.Code == SeedCatalog.MagicCode);

        // The order is the whole point of the list, and it is stored in a single column.
        Assert.Equal(
            [
                Tiebreaker.OpponentsMatchWinPercentage,
                Tiebreaker.GameWinPercentage,
                Tiebreaker.OpponentsGameWinPercentage
            ],
            magic.Tiebreakers);
    }

    [Fact]
    public async Task The_One_Piece_ruleset_keeps_its_differences_from_Magic()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        db.ChangeTracker.Clear();

        var onePiece = await db.Rulesets.SingleAsync(r => r.Tcg.Code == SeedCatalog.OnePieceCode);

        Assert.Equal(MatchFormat.Bo1, onePiece.SwissPhase.Format);
        Assert.Equal(TimeSpan.FromMinutes(5), onePiece.SwissPhase.ExtraTime);
        Assert.Equal(MatchFormat.Bo3, onePiece.TopCutPhase.Format);
        Assert.False(onePiece.ByesCountInOwnWinPercentage);
        Assert.Equal(Tiebreaker.OwnMatchWinPercentage, onePiece.Tiebreakers[0]);
    }

    [Fact]
    public async Task Reordering_the_tiebreakers_is_detected_and_persisted()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);

        var onePiece = await db.Rulesets.SingleAsync(r => r.Tcg.Code == SeedCatalog.OnePieceCode);
        onePiece.SetTiebreakers([Tiebreaker.OpponentsMatchWinPercentage, Tiebreaker.OwnMatchWinPercentage]);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.Rulesets.SingleAsync(r => r.Tcg.Code == SeedCatalog.OnePieceCode);

        // Without a ValueComparer on the converted list, EF would not notice this change at all.
        Assert.Equal(
            [Tiebreaker.OpponentsMatchWinPercentage, Tiebreaker.OwnMatchWinPercentage],
            reloaded.Tiebreakers);

        onePiece.SetTiebreakers(SeedCatalog.OnePieceRuleset(onePiece.TcgId).Tiebreakers);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_attendance_table_survives_a_round_trip()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        db.ChangeTracker.Clear();

        var magic = await db.Rulesets.SingleAsync(r => r.Tcg.Code == SeedCatalog.MagicCode);
        var onePiece = await db.Rulesets.SingleAsync(r => r.Tcg.Code == SeedCatalog.OnePieceCode);

        // Twelve players: five rounds and a top 4 in Magic, four rounds and a top 2 in One Piece.
        // Both tables come back out of one column apiece, order and nulls intact.
        Assert.Equal((5, 4), (magic.RecommendationFor(12)!.Value.SwissRounds, magic.RecommendationFor(12)!.Value.TopCutSize));
        Assert.Equal((4, 2), (onePiece.RecommendationFor(12)!.Value.SwissRounds, onePiece.RecommendationFor(12)!.Value.TopCutSize));

        // The band with no cut round-trips its null rather than becoming a zero.
        Assert.Null(magic.RecommendationFor(6)!.Value.TopCutSize);
    }

    [Fact]
    public async Task Two_TCGs_cannot_share_a_code()
    {
        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        db.ChangeTracker.Clear();

        db.Tcgs.Add(new Tcg(SeedCatalog.MagicCode, "Magic duplicado"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
