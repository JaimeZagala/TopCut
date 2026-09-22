using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Application.Tournaments;
using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Pairings;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// Seasonal leagues and which tournaments score towards them (ADR-0036), against real SQL Server.
/// What matters here is the picker and the check behind it agreeing: a league of another game, of
/// another store or already closed has to be refused on the way in, not merely hidden from a list.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class LeagueTests(SqlServerFixture fixture)
{
    // --- The league itself ------------------------------------------------------------------------

    [Fact]
    public async Task A_new_league_starts_open_and_with_no_tournaments()
    {
        var store = await NewStore();

        var created = await Leagues(store).CreateAsync(await Draft(store, "Liga de invierno"));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));

        var league = await Leagues(store).DetailsAsync(created.LeagueId!.Value);

        Assert.NotNull(league);
        Assert.True(league!.IsOpen);
        Assert.Equal("Liga de invierno", league.Name);
        Assert.Empty(league.Tournaments);
    }

    [Fact]
    public async Task A_season_cannot_end_before_it_starts()
    {
        var store = await NewStore();
        var magic = await GameId(SeedCatalog.MagicCode);

        var refused = await Leagues(store).CreateAsync(new LeagueDraft(
            magic,
            "Liga imposible",
            new DateOnly(2026, 12, 1),
            new DateOnly(2026, 10, 1)));

        Assert.False(refused.Succeeded);
        Assert.Contains("no puede terminar antes de empezar", string.Join(" ", refused.Errors));
        Assert.Empty(await Leagues(store).ForCurrentStoreAsync());
    }

    [Fact]
    public async Task Renaming_and_moving_the_season_leaves_the_tournaments_where_they_are()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var tournament = await NewTournament(store);

        Assert.True((await Setup(store).AssignToLeagueAsync(tournament, league)).Succeeded);

        var moved = await Leagues(store).UpdateAsync(league, new LeagueDraft(
            await GameId(SeedCatalog.MagicCode),
            "Liga de primavera",
            new DateOnly(2027, 3, 1),
            new DateOnly(2027, 5, 31)));

        Assert.True(moved.Succeeded, string.Join(" ", moved.Errors));

        var after = await Leagues(store).DetailsAsync(league);

        Assert.Equal("Liga de primavera", after!.Name);
        Assert.Equal(new DateOnly(2027, 3, 1), after.StartsOn);
        Assert.Single(after.Tournaments);
    }

    [Fact]
    public async Task Another_stores_league_is_not_in_your_list_and_cannot_be_read()
    {
        var mine = await NewStore();
        var theirs = await NewStore();

        var hidden = await NewLeague(theirs);

        Assert.Empty(await Leagues(mine).ForCurrentStoreAsync());
        Assert.Null(await Leagues(mine).DetailsAsync(hidden));
        Assert.Single(await Leagues(theirs).ForCurrentStoreAsync());
    }

    // --- Which tournaments score --------------------------------------------------------------------

    [Fact]
    public async Task A_tournament_joins_a_league_and_can_leave_it_again()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var tournament = await NewTournament(store);

        Assert.True((await Setup(store).AssignToLeagueAsync(tournament, league)).Succeeded);

        var row = Assert.Single((await Leagues(store).DetailsAsync(league))!.Tournaments);

        Assert.Equal(tournament, row.Id);
        Assert.Equal(league, (await Queries(store).DetailsAsync(tournament))!.LeagueId);

        Assert.True((await Setup(store).AssignToLeagueAsync(tournament, null)).Succeeded);

        Assert.Empty((await Leagues(store).DetailsAsync(league))!.Tournaments);
        Assert.Null((await Queries(store).DetailsAsync(tournament))!.LeagueId);
    }

    [Fact]
    public async Task A_tournament_already_under_way_can_still_be_added_to_a_league()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var tournament = await NewTournament(store);

        await StartIt(store, tournament);

        // The store ran three of these and then decided they were a season. Assigning changes which
        // table adds the points up, never anything already played, so it is allowed.
        var joined = await Setup(store).AssignToLeagueAsync(tournament, league);

        Assert.True(joined.Succeeded, string.Join(" ", joined.Errors));
        Assert.Single((await Leagues(store).DetailsAsync(league))!.Tournaments);
    }

    [Fact]
    public async Task A_cancelled_tournament_scores_for_nobody()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var tournament = await NewTournament(store);

        Assert.True((await Setup(store).CancelAsync(tournament)).Succeeded);

        var refused = await Setup(store).AssignToLeagueAsync(tournament, league);

        Assert.False(refused.Succeeded);
        Assert.Contains("cancelado", string.Join(" ", refused.Errors));
    }

    [Fact]
    public async Task A_league_of_another_game_is_neither_offered_nor_accepted()
    {
        var store = await NewStore();
        var onePiece = await NewLeague(store, SeedCatalog.OnePieceCode);
        var tournament = await NewTournament(store);

        Assert.Empty(await Leagues(store).OpenForAsync(await GameId(SeedCatalog.MagicCode)));

        var refused = await Setup(store).AssignToLeagueAsync(tournament, onePiece);

        Assert.False(refused.Succeeded);
        Assert.Contains("no admite este torneo", string.Join(" ", refused.Errors));
    }

    [Fact]
    public async Task Another_stores_league_cannot_take_your_tournament()
    {
        var mine = await NewStore();
        var theirs = await NewStore();

        var hidden = await NewLeague(theirs);
        var tournament = await NewTournament(mine);

        var refused = await Setup(mine).AssignToLeagueAsync(tournament, hidden);

        Assert.False(refused.Succeeded);
        Assert.Contains("no admite este torneo", string.Join(" ", refused.Errors));
    }

    // --- Closing ------------------------------------------------------------------------------------

    [Fact]
    public async Task A_closed_league_stops_taking_tournaments_but_keeps_the_ones_it_had()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var played = await NewTournament(store);
        var latecomer = await NewTournament(store);

        Assert.True((await Setup(store).AssignToLeagueAsync(played, league)).Succeeded);
        Assert.True((await Leagues(store).CloseAsync(league)).Succeeded);

        var refused = await Setup(store).AssignToLeagueAsync(latecomer, league);

        Assert.False(refused.Succeeded);
        Assert.Contains("no admite este torneo", string.Join(" ", refused.Errors));

        // What it already had still counts, and the season's table is still there to look at.
        Assert.Single((await Leagues(store).DetailsAsync(league))!.Tournaments);
    }

    [Fact]
    public async Task The_picker_hides_a_closed_league_unless_the_tournament_is_already_in_it()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var tournament = await NewTournament(store);
        var magic = await GameId(SeedCatalog.MagicCode);

        Assert.True((await Setup(store).AssignToLeagueAsync(tournament, league)).Succeeded);
        Assert.True((await Leagues(store).CloseAsync(league)).Succeeded);

        // Without this the tournament's own screen would open showing "ninguna" and drop the league
        // the next time anything on it was saved.
        Assert.Empty(await Leagues(store).OpenForAsync(magic));

        var offered = Assert.Single(await Leagues(store).OpenForAsync(magic, include: league));

        Assert.Equal(league, offered.Id);
        Assert.False(offered.IsOpen);
    }

    [Fact]
    public async Task A_league_can_be_reopened_and_takes_tournaments_again()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var tournament = await NewTournament(store);

        Assert.True((await Leagues(store).CloseAsync(league)).Succeeded);
        Assert.True((await Leagues(store).ReopenAsync(league)).Succeeded);
        Assert.True((await Setup(store).AssignToLeagueAsync(tournament, league)).Succeeded);

        Assert.False((await Leagues(store).ReopenAsync(league)).Succeeded);
    }

    // --- Harness ------------------------------------------------------------------------------------

    private ILeagues Leagues(Guid storeId) => fixture.CreateLeagues(storeId);

    private ITournamentQueries Queries(Guid storeId) =>
        new TournamentQueries(fixture.CreateContextFactory(storeId), TimeProvider.System);

    private TournamentSetupService Setup(Guid storeId) => new(
        new TournamentRepository(fixture.CreateContextFactory(storeId)),
        new TcgCatalog(fixture.CreateContextFactory(storeId)),
        fixture.CreateLeagues(storeId),
        new StoreScope(storeId));

    private async Task<LeagueDraft> Draft(Guid storeId, string name, string code = SeedCatalog.MagicCode) =>
        new(await GameId(code), name, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31));

    private async Task<Guid> NewLeague(Guid storeId, string code = SeedCatalog.MagicCode)
    {
        var name = "Liga " + Guid.CreateVersion7().ToString("N");
        var created = await Leagues(storeId).CreateAsync(await Draft(storeId, name, code));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        return created.LeagueId!.Value;
    }

    private async Task<int> GameId(string code)
    {
        await using var db = fixture.CreateContext();

        await CatalogSeeder.SeedAsync(db);

        return (await db.Tcgs.SingleAsync(tcg => tcg.Code == code)).Id;
    }

    private async Task<Guid> NewStore()
    {
        await using var db = fixture.CreateContext();

        var store = new Store(
            "Tienda " + Guid.CreateVersion7().ToString("N"),
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"));

        store.Approve();

        db.Stores.Add(store);
        await db.SaveChangesAsync();

        return store.Id;
    }

    private async Task<Guid> NewTournament(Guid storeId)
    {
        var created = await Setup(storeId).CreateAsync(new TournamentDraft(
            await GameId(SeedCatalog.MagicCode),
            "Torneo de liga",
            DateTimeOffset.UtcNow.AddDays(7),
            "Calle Mayor 1",
            "Valencia",
            "Valencia",
            "46001",
            Capacity: 8,
            SwissRounds: 3,
            TopCutSize: null));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        return created.TournamentId!.Value;
    }

    /// <summary>Publishes it, fills four seats and opens round one, so the settings are frozen.</summary>
    private async Task StartIt(Guid storeId, Guid tournamentId)
    {
        var repository = new TournamentRepository(fixture.CreateContextFactory(storeId));

        var started = await repository.ExecuteAsync(tournamentId, tournament =>
        {
            tournament.Publish();

            for (var seat = 1; seat <= 4; seat++)
            {
                tournament.SignUp("Jugador " + seat);
            }

            TournamentEngine.StartNextRound(tournament, DateTimeOffset.UtcNow);
            return OperationResult.Ok();
        });

        Assert.True(started.Succeeded, string.Join(" ", started.Errors));
    }

    private sealed class StoreScope(Guid storeId) : ITenantContext
    {
        public Guid? TenantId { get; } = storeId;
    }
}
