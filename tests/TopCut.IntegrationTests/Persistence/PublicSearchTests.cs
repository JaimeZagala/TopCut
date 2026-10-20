using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// The public search, against real PostgreSQL. This is the one place in the application that reads
/// across stores on purpose (ADR-0029), so these tests pull in both directions: that a visitor with
/// no store in scope <b>does</b> see other people's tournaments, and that the three things which
/// must stay hidden — drafts, unapproved stores and test tenants — stay hidden.
/// <para>
/// Every test gives its store its own invented city and searches by it. The suite shares one
/// database, so a search with no filters would pick up whatever another test class left behind.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public class PublicSearchTests(PostgresFixture fixture)
{
    // --- What is public and what is not -----------------------------------------------------------

    [Fact]
    public async Task A_visitor_with_no_store_in_scope_sees_a_published_tournament_from_any_store()
    {
        var city = NewCity();
        var store = await NewStore(city);
        await NewTournament(store, city, name: "Prerelease de invierno", published: true);

        // No tenant at all: an anonymous browser, which is where the tenant filter would normally
        // fail closed and return nothing.
        var row = Assert.Single(await Anonymous().SearchAsync(new TournamentSearch { City = city }));

        Assert.Equal("Prerelease de invierno", row.Name);
        Assert.Equal("Magic: The Gathering", row.TcgName);
        Assert.Equal(city, row.City);
    }

    [Fact]
    public async Task Another_stores_scope_does_not_narrow_the_public_search()
    {
        var city = NewCity();
        var store = await NewStore(city);
        var somebodyElse = await NewStore(NewCity());

        await NewTournament(store, city, published: true);

        // A signed-in store browsing the public listing is still browsing the public listing.
        var results = await Public(somebodyElse).SearchAsync(new TournamentSearch { City = city });

        Assert.Single(results);
    }

    [Fact]
    public async Task A_draft_is_nobody_elses_business_until_the_store_publishes_it()
    {
        var city = NewCity();
        var store = await NewStore(city);
        var id = await NewTournament(store, city, name: "Sin publicar", published: false);

        Assert.Empty(await Anonymous().SearchAsync(new TournamentSearch { City = city }));
        Assert.Null(await Anonymous().FindAsync(id));

        await Publish(store, id);

        Assert.Single(await Anonymous().SearchAsync(new TournamentSearch { City = city }));
        Assert.NotNull(await Anonymous().FindAsync(id));
    }

    [Fact]
    public async Task A_cancelled_tournament_stays_visible_because_somebody_had_it_in_their_diary()
    {
        var city = NewCity();
        var store = await NewStore(city);
        var id = await NewTournament(store, city, name: "Se suspende", published: true);

        await Cancel(store, id);

        var sheet = await Anonymous().FindAsync(id);

        Assert.NotNull(sheet);
        Assert.Equal(TournamentStatus.Cancelled, sheet!.Status);

        // Hiding it would leave the player who signed up staring at a broken link the morning of.
        Assert.False(sheet.AcceptsRegistrations);
        Assert.Single(await Anonymous().SearchAsync(new TournamentSearch { City = city }));
    }

    [Fact]
    public async Task A_store_that_is_not_approved_right_now_publishes_to_nobody()
    {
        var waiting = NewCity();
        var revoked = NewCity();

        var pending = await NewStore(waiting, approved: false);
        var cutOff = await NewStore(revoked);

        await NewTournament(pending, waiting, published: true);
        var id = await NewTournament(cutOff, revoked, published: true);

        Assert.Empty(await Anonymous().SearchAsync(new TournamentSearch { City = waiting }));

        // Approved when it was published, revoked afterwards: it has to disappear on the next
        // request, which is why the check is a join and not a flag copied onto the tournament.
        Assert.Single(await Anonymous().SearchAsync(new TournamentSearch { City = revoked }));

        await Revoke(cutOff);

        Assert.Empty(await Anonymous().SearchAsync(new TournamentSearch { City = revoked }));
        Assert.Null(await Anonymous().FindAsync(id));
    }

    [Fact]
    public async Task A_test_store_is_kept_out_of_the_public_search()
    {
        var city = NewCity();
        var store = await NewStore(city, isTestStore: true);
        var id = await NewTournament(store, city, published: true);

        // It stands in for the staging environment the Azure free tier cannot fund, so its
        // tournaments must never reach a player.
        Assert.Empty(await Anonymous().SearchAsync(new TournamentSearch { City = city }));
        Assert.Null(await Anonymous().FindAsync(id));
    }

    // --- Filters --------------------------------------------------------------------------------

    [Fact]
    public async Task The_search_filters_by_game()
    {
        var city = NewCity();
        var store = await NewStore(city);

        await NewTournament(store, city, name: "Noche de Magic", published: true);
        await NewTournament(store, city, name: "Noche de One Piece", game: SeedCatalog.OnePieceCode, published: true);

        var onePiece = await GameId(SeedCatalog.OnePieceCode);
        var row = Assert.Single(await Anonymous().SearchAsync(new TournamentSearch { City = city, TcgId = onePiece }));

        Assert.Equal("Noche de One Piece", row.Name);
    }

    [Fact]
    public async Task The_text_matches_the_store_name_as_well_as_the_tournament()
    {
        var city = NewCity();
        var store = await NewStore(city, name: "Kraken Cards");

        await NewTournament(store, city, name: "Torneo del sábado", published: true);

        Assert.Single(await Anonymous().SearchAsync(new TournamentSearch { City = city, Text = "sábado" }));

        // "The one at Kraken" is how a player remembers it as often as by the event's name.
        Assert.Single(await Anonymous().SearchAsync(new TournamentSearch { City = city, Text = "kraken" }));

        Assert.Empty(await Anonymous().SearchAsync(new TournamentSearch { City = city, Text = "domingo" }));
    }

    [Fact]
    public async Task The_search_filters_by_date_range()
    {
        var city = NewCity();
        var store = await NewStore(city);
        var now = DateTimeOffset.UtcNow;

        await NewTournament(store, city, name: "Mañana", startsAt: now.AddDays(1), published: true);
        await NewTournament(store, city, name: "El mes que viene", startsAt: now.AddDays(30), published: true);

        var soon = await Anonymous().SearchAsync(new TournamentSearch { City = city, Until = now.AddDays(7) });

        Assert.Equal("Mañana", Assert.Single(soon).Name);

        var later = await Anonymous().SearchAsync(new TournamentSearch { City = city, From = now.AddDays(7) });

        Assert.Equal("El mes que viene", Assert.Single(later).Name);
    }

    [Fact]
    public async Task Upcoming_reads_forwards_and_past_reads_backwards()
    {
        var city = NewCity();
        var store = await NewStore(city);
        var now = DateTimeOffset.UtcNow;

        await NewTournament(store, city, name: "Dentro de un mes", startsAt: now.AddDays(30), published: true);
        await NewTournament(store, city, name: "Mañana", startsAt: now.AddDays(1), published: true);
        await NewTournament(store, city, name: "Ayer", startsAt: now.AddDays(-1), published: true);
        await NewTournament(store, city, name: "El mes pasado", startsAt: now.AddDays(-30), published: true);

        var upcoming = await Anonymous().SearchAsync(new TournamentSearch { City = city });

        Assert.Equal(["Mañana", "Dentro de un mes"], upcoming.Select(row => row.Name));

        // The past is not the same list reversed: it is a different question, and the answer
        // nearest to now goes first there too.
        var past = await Anonymous().SearchAsync(new TournamentSearch { City = city, Past = true });

        Assert.Equal(["Ayer", "El mes pasado"], past.Select(row => row.Name));
    }

    [Fact]
    public async Task The_city_list_only_offers_cities_that_have_something_to_show()
    {
        var published = NewCity();
        var draftOnly = NewCity();

        await NewTournament(await NewStore(published), published, published: true);
        await NewTournament(await NewStore(draftOnly), draftOnly, published: false);

        var cities = await Anonymous().CitiesAsync();

        Assert.Contains(published, cities);
        Assert.DoesNotContain(draftOnly, cities);
    }

    // --- The sheet -------------------------------------------------------------------------------

    [Fact]
    public async Task The_sheet_counts_seats_and_the_waitlist_of_a_store_that_is_not_the_visitors()
    {
        var city = NewCity();
        var store = await NewStore(city, name: "Kraken Cards");
        var id = await NewTournament(store, city, capacity: 4, published: true);

        await RegisterPlayers(store, id, 6);

        // The participant rows carry the tenant filter too. Without the same opt-out the counts
        // would come back as zero and the sheet would promise an empty tournament.
        var sheet = await Anonymous().FindAsync(id);

        Assert.NotNull(sheet);
        Assert.Equal("Kraken Cards", sheet!.StoreName);
        Assert.Equal(4, sheet.Registered);
        Assert.Equal(2, sheet.Waitlisted);
        Assert.True(sheet.IsFull);
        Assert.Equal(0, sheet.SeatsLeft);
    }

    [Fact]
    public async Task The_sheet_carries_the_rules_the_tournament_will_actually_be_played_under()
    {
        var city = NewCity();
        var store = await NewStore(city);
        var id = await NewTournament(store, city, published: true);

        var sheet = await Anonymous().FindAsync(id);

        // Copied from the Magic ruleset when the tournament was created, not read from the
        // rulebook as it stands today.
        Assert.Equal(MatchFormat.Bo3, sheet!.SwissPhase.Format);
        Assert.Equal(TimeSpan.FromMinutes(50), sheet.SwissPhase.RoundLength);
        Assert.Contains("Magic Tournament Rules", sheet.RulesVersion);
    }

    [Fact]
    public async Task A_tournament_that_does_not_exist_has_no_sheet()
    {
        Assert.Null(await Anonymous().FindAsync(Guid.CreateVersion7()));
    }

    // --- Harness ---------------------------------------------------------------------------------

    /// <summary>The search as an anonymous visitor runs it: no store in scope at all.</summary>
    private IPublicTournaments Anonymous() => Public(null);

    private IPublicTournaments Public(Guid? tenantId) =>
        new PublicTournaments(fixture.CreateContextFactory(tenantId), TimeProvider.System);

    /// <summary>A city nobody else's test is using, so each search can be pinned to one store.</summary>
    private static string NewCity() => $"Villa {Guid.CreateVersion7():N}";

    private async Task<Guid> NewStore(
        string city,
        string? name = null,
        bool approved = true,
        bool isTestStore = false)
    {
        await using var db = fixture.CreateContext();

        var store = new Store(
            name ?? $"Tienda {Guid.CreateVersion7():N}",
            new Address("Calle Mayor 1", city, "Valencia", "46001"),
            isTestStore);

        if (approved)
        {
            store.Approve();
        }

        db.Stores.Add(store);
        await db.SaveChangesAsync();

        return store.Id;
    }

    private async Task Revoke(Guid storeId)
    {
        await using var db = fixture.CreateContext();

        var store = await db.Stores.SingleAsync(candidate => candidate.Id == storeId);
        store.RevokeApproval();

        await db.SaveChangesAsync();
    }

    private async Task<Guid> NewTournament(
        Guid storeId,
        string city,
        string name = "Torneo de prueba",
        string game = SeedCatalog.MagicCode,
        DateTimeOffset? startsAt = null,
        int capacity = 16,
        bool published = false)
    {
        await using var db = fixture.CreateContext(storeId);

        await CatalogSeeder.SeedAsync(db);

        var ruleset = await db.Rulesets
            .Include(candidate => candidate.Tcg)
            .SingleAsync(candidate => candidate.Tcg.Code == game);

        var tournament = Tournament.Create(
            storeId,
            ruleset,
            name,
            startsAt ?? DateTimeOffset.UtcNow.AddDays(7),
            new Address("Calle Mayor 1", city, "Valencia", "46001"),
            capacity,
            swissRounds: 4);

        if (published)
        {
            tournament.Publish();
        }

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return tournament.Id;
    }

    private Task Publish(Guid storeId, Guid tournamentId) => Change(storeId, tournamentId, t => t.Publish());

    private Task Cancel(Guid storeId, Guid tournamentId) => Change(storeId, tournamentId, t => t.Cancel());

    private Task RegisterPlayers(Guid storeId, Guid tournamentId, int players) =>
        Change(storeId, tournamentId, tournament =>
        {
            for (var seat = 1; seat <= players; seat++)
            {
                tournament.SignUp($"Jugador {seat}");
            }
        });

    private async Task Change(Guid storeId, Guid tournamentId, Action<Tournament> change)
    {
        var repository = new TournamentRepository(fixture.CreateContextFactory(storeId));

        var result = await repository.ExecuteAsync(tournamentId, tournament =>
        {
            change(tournament);
            return OperationResult.Ok();
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    private async Task<int> GameId(string code)
    {
        await using var db = fixture.CreateContext();

        await CatalogSeeder.SeedAsync(db);

        return (await db.Tcgs.SingleAsync(tcg => tcg.Code == code)).Id;
    }
}
