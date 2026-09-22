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
/// The season's cumulative table and the league's public page (ADR-0037), against real SQL Server.
/// Two things are worth the round trip: that the same person's points really do come back added up
/// across tournaments through their book entry, and that what a visitor sees is only what the store
/// has published.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class LeagueStandingsTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task The_season_adds_up_what_the_same_person_scored_in_every_tournament()
    {
        var store = await NewStore();
        var league = await NewLeague(store);

        await PlayAsync(store, await NewTournament(store, league), winner: "Ana Pérez", loser: "Bea Ruiz");
        await PlayAsync(store, await NewTournament(store, league), winner: "Ana Pérez", loser: "Bea Ruiz");

        var table = await Leagues(store).StandingsAsync(league);

        Assert.Equal(2, table.Count);

        var ana = table[0];

        Assert.Equal("Ana Pérez", ana.Name);
        Assert.Equal(1, ana.Rank);
        Assert.Equal(6, ana.Points);
        Assert.Equal(2, ana.Tournaments);

        Assert.Equal("Bea Ruiz", table[1].Name);
        Assert.Equal(0, table[1].Points);
    }

    [Fact]
    public async Task Merging_two_entries_joins_their_seasons()
    {
        var store = await NewStore();
        var league = await NewLeague(store);

        await PlayAsync(store, await NewTournament(store, league), winner: "Ana Pérez", loser: "Bea Ruiz");

        // The same Ana, written down a second time, which is what the book is there to fix.
        var second = await NewTournament(store, league);

        await Counter(store).RegisterAsync(second, "Ana Pérez", asSomebodyNew: true);
        await PlayAsync(store, second, winner: "Ana Pérez", loser: "Bea Ruiz", registerWinner: false);

        Assert.Equal(3, (await Leagues(store).StandingsAsync(league)).Count);

        var book = await Book(store).AllAsync();
        var anas = book.Where(row => row.Name == "Ana Pérez").ToList();

        Assert.True((await Book(store).MergeAsync(anas[0].Id, anas[1].Id)).Succeeded);

        var table = await Leagues(store).StandingsAsync(league);

        Assert.Equal(2, table.Count);
        Assert.Equal(6, table[0].Points);
        Assert.Equal(2, table[0].Tournaments);
    }

    [Fact]
    public async Task A_draft_tournament_is_on_the_stores_page_and_not_on_the_public_one()
    {
        var store = await NewStore();
        var league = await NewLeague(store);

        await PlayAsync(store, await NewTournament(store, league), winner: "Ana Pérez", loser: "Bea Ruiz");

        // Next month's event, still the store's own workspace, with people already written in.
        var draft = await NewTournament(store, league);

        await Counter(store).RegisterAsync(draft, "Cris Soler");

        Assert.Equal(2, (await Leagues(store).DetailsAsync(league))!.Tournaments.Count);

        var page = await Public().FindAsync(league);

        Assert.NotNull(page);
        Assert.Single(page!.Tournaments);
        Assert.DoesNotContain(page.Tournaments, row => row.Id == draft);
    }

    [Fact]
    public async Task A_league_whose_tournaments_are_all_drafts_has_no_public_page()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var draft = await NewTournament(store, league);

        await Counter(store).RegisterAsync(draft, "Ana Pérez");

        Assert.False(await Public().IsPublicAsync(league));
        Assert.Null(await Public().FindAsync(league));
    }

    [Fact]
    public async Task Revoking_a_store_takes_its_league_off_the_public_page()
    {
        var store = await NewStore();
        var league = await NewLeague(store);

        await PlayAsync(store, await NewTournament(store, league), winner: "Ana Pérez", loser: "Bea Ruiz");

        Assert.NotNull(await Public().FindAsync(league));

        await RevokeAsync(store);

        // Same rule as the public search, asked of the league: nothing cached, nothing to expire.
        Assert.Null(await Public().FindAsync(league));
        Assert.False(await Public().IsPublicAsync(league));
    }

    [Fact]
    public async Task Another_stores_league_has_no_table_here()
    {
        var mine = await NewStore();
        var theirs = await NewStore();
        var league = await NewLeague(theirs);

        await PlayAsync(theirs, await NewTournament(theirs, league), winner: "Ana Pérez", loser: "Bea Ruiz");

        Assert.Empty(await Leagues(mine).StandingsAsync(league));
        Assert.Equal(2, (await Leagues(theirs).StandingsAsync(league)).Count);
    }

    [Fact]
    public async Task The_tournament_sheet_says_which_league_it_scores_for()
    {
        var store = await NewStore();
        var league = await NewLeague(store, "Liga de invierno");
        var tournament = await NewTournament(store, league);

        await PlayAsync(store, tournament, winner: "Ana Pérez", loser: "Bea Ruiz");

        var sheet = await Sheets().FindAsync(tournament);

        Assert.NotNull(sheet);
        Assert.Equal(league, sheet!.LeagueId);
        Assert.Equal("Liga de invierno", sheet.LeagueName);
    }

    // --- Lo que ve el jugador -----------------------------------------------------------------------

    [Fact]
    public async Task A_player_sees_the_leagues_they_have_scored_in_wherever_they_play()
    {
        var mine = await NewStore();
        var theirs = await NewStore();
        var account = NewAccount();

        var here = await NewLeague(mine, "Liga de Kraken");
        var there = await NewLeague(theirs, "Liga del Dragón");

        await PlayAsync(mine, await NewTournament(mine, here), winner: "Ana Pérez", loser: "Bea Ruiz", account);
        await PlayAsync(theirs, await NewTournament(theirs, there), winner: "Bea Ruiz", loser: "Ana Pérez", account);

        var rows = await Public().ForPlayerAsync(account);

        Assert.Equal(2, rows.Count);

        var won = rows.Single(row => row.LeagueId == here);

        Assert.Equal("Liga de Kraken", won.Name);
        Assert.Equal(1, won.Rank);
        Assert.Equal(2, won.Players);
        Assert.Equal(3, won.Points);
        Assert.Equal(1, won.Tournaments);

        // The same account, second in the other store's league. The book is per store; the account
        // is what ties the two together (ADR-0035).
        Assert.Equal(2, rows.Single(row => row.LeagueId == there).Rank);
    }

    [Fact]
    public async Task A_league_you_signed_up_for_but_have_not_played_is_not_yours_yet()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var account = NewAccount();
        var announced = await NewTournament(store, league);

        // On the list for a tournament that has not started, so there is nothing to stand in.
        await Counter(store).RegisterAsync(announced, "Ana Pérez");
        await Book(store).ForPlayerAsync(store, account, "Ana Pérez");

        Assert.Empty(await Public().ForPlayerAsync(account));
    }

    [Fact]
    public async Task The_table_marks_which_row_belongs_to_whoever_is_reading_it()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var account = NewAccount();

        await PlayAsync(store, await NewTournament(store, league), winner: "Ana Pérez", loser: "Bea Ruiz", account);

        var anonymous = await Public().FindAsync(league);
        var signedIn = await Public().FindAsync(league, account);

        Assert.Null(anonymous!.Viewer);
        Assert.NotNull(signedIn!.Viewer);

        // And it is the row they actually own, not just any row.
        Assert.Equal("Ana Pérez", signedIn.Standings.Single(row => row.StorePlayerId == signedIn.Viewer).Name);
    }

    [Fact]
    public async Task A_revoked_store_takes_its_league_off_the_players_list_too()
    {
        var store = await NewStore();
        var league = await NewLeague(store);
        var account = NewAccount();

        await PlayAsync(store, await NewTournament(store, league), winner: "Ana Pérez", loser: "Bea Ruiz", account);

        Assert.Single(await Public().ForPlayerAsync(account));

        await RevokeAsync(store);

        Assert.Empty(await Public().ForPlayerAsync(account));
    }

    // --- Harness ------------------------------------------------------------------------------------

    private static string NewAccount() => $"cuenta-{Guid.CreateVersion7():N}";

    private ILeagues Leagues(Guid storeId) => fixture.CreateLeagues(storeId);

    private IPublicLeagues Public() => fixture.CreatePublicLeagues();

    private IPublicTournaments Sheets() =>
        new PublicTournaments(fixture.CreateContextFactory(), TimeProvider.System);

    private IStorePlayers Book(Guid storeId) => fixture.CreateStorePlayers(storeId);

    private ParticipantService Counter(Guid storeId) => new(
        new TournamentRepository(fixture.CreateContextFactory(storeId)),
        fixture.CreateStorePlayers(storeId));

    private TournamentSetupService Setup(Guid storeId) => new(
        new TournamentRepository(fixture.CreateContextFactory(storeId)),
        new TcgCatalog(fixture.CreateContextFactory(storeId)),
        fixture.CreateLeagues(storeId),
        new StoreScope(storeId));

    private async Task<Guid> NewLeague(Guid storeId, string name = "Liga de prueba")
    {
        var created = await Leagues(storeId).CreateAsync(new LeagueDraft(
            await GameId(SeedCatalog.MagicCode),
            name,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 6, 30)));

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

    private async Task RevokeAsync(Guid storeId)
    {
        await using var db = fixture.CreateContext(storeId);

        var store = await db.Stores.SingleAsync(candidate => candidate.Id == storeId);

        store.RevokeApproval();
        await db.SaveChangesAsync();
    }

    private async Task<Guid> NewTournament(Guid storeId, Guid leagueId)
    {
        var created = await Setup(storeId).CreateAsync(new TournamentDraft(
            await GameId(SeedCatalog.MagicCode),
            "Jornada de liga",
            DateTimeOffset.UtcNow.AddDays(7),
            "Calle Mayor 1",
            "Valencia",
            "Valencia",
            "46001",
            Capacity: 8,
            SwissRounds: 1,
            TopCutSize: null));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));

        var tournamentId = created.TournamentId!.Value;
        var joined = await Setup(storeId).AssignToLeagueAsync(tournamentId, leagueId);

        Assert.True(joined.Succeeded, string.Join(" ", joined.Errors));
        return tournamentId;
    }

    /// <summary>
    /// Signs two of the store's regulars up at the counter, publishes, plays the single round and
    /// closes it. Everything through the real services, so the book entries are the real ones.
    /// </summary>
    private async Task PlayAsync(
        Guid storeId,
        Guid tournamentId,
        string winner,
        string loser,
        string? account = null,
        bool registerWinner = true)
    {
        if (registerWinner)
        {
            Assert.True((await Counter(storeId).RegisterAsync(tournamentId, winner)).Succeeded);
        }

        Assert.True((await Counter(storeId).RegisterAsync(tournamentId, loser)).Succeeded);

        // Gives the account the book entry the counter just used, which is what a store does when
        // the regular they have been writing down by hand finally registers.
        if (account is not null)
        {
            var entry = (await Book(storeId).AllAsync()).Single(row => row.Name == "Ana Pérez");

            await using var db = fixture.CreateContext(storeId);
            var player = await db.StorePlayers.SingleAsync(candidate => candidate.Id == entry.Id);

            if (player.UserId is null)
            {
                player.LinkAccount(account);
                await db.SaveChangesAsync();
            }
        }

        var repository = new TournamentRepository(fixture.CreateContextFactory(storeId));
        var now = DateTimeOffset.UtcNow;

        var played = await repository.ExecuteAsync(tournamentId, tournament =>
        {
            tournament.Publish();

            var round = TournamentEngine.StartNextRound(tournament, now);
            var pairing = round.Pairings.Single();
            var playerA = tournament.Participants.Single(p => p.Id == pairing.PlayerAId);

            var side = playerA.Name == winner ? GameWinner.PlayerA : GameWinner.PlayerB;

            tournament.ReportResult(round.Id, pairing.Id, [side, side], reportedBy: null);
            tournament.CloseRound(round.Id, now.AddHours(1));

            return OperationResult.Ok();
        });

        Assert.True(played.Succeeded, string.Join(" ", played.Errors));
    }

    private sealed class StoreScope(Guid storeId) : ITenantContext
    {
        public Guid? TenantId { get; } = storeId;
    }
}
