using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Application.Tournaments;
using TopCut.Domain.Catalog;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// The store's book of regulars (ADR-0035), against real SQL Server. What matters here is <b>who
/// gets folded together and who does not</b>: a league adds up whatever this says is one person, so
/// a wrong merge quietly hands somebody else's points to the wrong player.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class StoreBookTests(SqlServerFixture fixture)
{
    // --- What counts as the same person ------------------------------------------------------------

    [Fact]
    public async Task The_same_name_typed_at_two_tournaments_is_one_person()
    {
        var store = await NewStore();
        var january = await NewTournament(store);
        var february = await NewTournament(store);

        await Counter(store).RegisterAsync(january, "Ana Pérez");
        await Counter(store).RegisterAsync(february, "Ana Pérez");

        var ana = Assert.Single(await Book(store).AllAsync());

        Assert.Equal("Ana Pérez", ana.Name);
        Assert.Equal(2, ana.Tournaments);
    }

    [Fact]
    public async Task Accents_and_stray_spaces_do_not_make_a_second_person()
    {
        var store = await NewStore();
        var january = await NewTournament(store);
        var february = await NewTournament(store);

        await Counter(store).RegisterAsync(january, "Ana Pérez");
        await Counter(store).RegisterAsync(february, "  ana perez ");

        // Typed in a hurry at a counter. One person, and the first spelling is the one kept.
        var ana = Assert.Single(await Book(store).AllAsync());

        Assert.Equal("Ana Pérez", ana.Name);
        Assert.Equal(2, ana.Tournaments);
    }

    [Fact]
    public async Task Two_stores_keep_separate_books()
    {
        var mine = await NewStore();
        var theirs = await NewStore();

        await Counter(mine).RegisterAsync(await NewTournament(mine), "Ana Pérez");
        await Counter(theirs).RegisterAsync(await NewTournament(theirs), "Ana Pérez");

        Assert.Single(await Book(mine).AllAsync());
        Assert.Single(await Book(theirs).AllAsync());
    }

    [Fact]
    public async Task A_shop_with_two_Anas_can_keep_them_apart_on_purpose()
    {
        var store = await NewStore();
        var tournament = await NewTournament(store);

        await Counter(store).RegisterAsync(tournament, "Ana");
        await Counter(store).RegisterAsync(tournament, "Ana", asSomebodyNew: true);

        var book = await Book(store).AllAsync();

        Assert.Equal(2, book.Count);

        // And the screen points at the pair rather than deciding for the store.
        Assert.All(book, row => Assert.NotNull(row.PossibleDuplicateOf));
    }

    [Fact]
    public async Task An_online_sign_up_is_never_folded_into_a_walk_in_with_the_same_name()
    {
        var store = await NewStore();
        var tournament = await NewTournament(store, published: true);

        await Counter(store).RegisterAsync(tournament, "Ana Pérez");
        await Player().JoinAsync(await NewTournament(store, published: true), NewAccount(), "Ana Pérez");

        // Nobody is standing in the room to catch it if this guesses wrong, so it does not guess:
        // two entries, flagged as a possible duplicate for the store to resolve.
        var book = await Book(store).AllAsync();

        Assert.Equal(2, book.Count);
        Assert.Single(book, row => row.HasAccount);
        Assert.All(book, row => Assert.NotNull(row.PossibleDuplicateOf));
    }

    [Fact]
    public async Task The_same_account_at_two_tournaments_is_one_person_whatever_they_are_called()
    {
        var store = await NewStore();
        var account = NewAccount();

        await Player().JoinAsync(await NewTournament(store, published: true), account, "Ana Pérez");
        await Player().JoinAsync(await NewTournament(store, published: true), account, "Ana P.");

        // The account is the link that cannot be wrong, so a changed display name is still them.
        var ana = Assert.Single(await Book(store).AllAsync());

        Assert.True(ana.HasAccount);
        Assert.Equal(2, ana.Tournaments);
    }

    // --- The counter's suggestions -------------------------------------------------------------------

    [Fact]
    public async Task The_counter_is_offered_who_it_already_knows_best_match_first()
    {
        var store = await NewStore();
        var tournament = await NewTournament(store);

        await Counter(store).RegisterAsync(tournament, "Ana Pérez");
        await Counter(store).RegisterAsync(tournament, "Mariana Gil");

        var suggestions = await Book(store).SearchAsync("ana");

        Assert.Equal(2, suggestions.Count);

        // Typing "ana" should offer Ana before Mariana, even though both contain it.
        Assert.Equal("Ana Pérez", suggestions[0].Name);
    }

    [Fact]
    public async Task Nothing_typed_suggests_nothing()
    {
        var store = await NewStore();

        await Counter(store).RegisterAsync(await NewTournament(store), "Ana Pérez");

        Assert.Empty(await Book(store).SearchAsync("   "));
    }

    // --- Corrections ------------------------------------------------------------------------------------

    [Fact]
    public async Task Correcting_a_name_changes_the_book_and_leaves_played_tournaments_alone()
    {
        var store = await NewStore();
        var tournament = await NewTournament(store);

        await Counter(store).RegisterAsync(tournament, "Ana Perze");

        var ana = Assert.Single(await Book(store).AllAsync());

        Assert.True((await Book(store).RenameAsync(ana.Id, "Ana Pérez")).Succeeded);

        Assert.Equal("Ana Pérez", Assert.Single(await Book(store).AllAsync()).Name);

        // The roster of a tournament already played keeps what it said at the time.
        var roster = await new TournamentQueries(fixture.CreateContextFactory(store), TimeProvider.System)
            .RosterAsync(tournament);

        Assert.Equal("Ana Perze", Assert.Single(roster.Registered).Name);

        // And the corrected spelling is what the counter finds from now on.
        Assert.Single(await Book(store).SearchAsync("perez"));
    }

    [Fact]
    public async Task A_player_from_another_store_cannot_be_renamed()
    {
        var mine = await NewStore();
        var theirs = await NewStore();

        await Counter(theirs).RegisterAsync(await NewTournament(theirs), "Ana Pérez");

        var ana = Assert.Single(await Book(theirs).AllAsync());
        var refused = await Book(mine).RenameAsync(ana.Id, "Otra cosa");

        Assert.False(refused.Succeeded);
        Assert.Contains("no está en tu fichero", string.Join(" ", refused.Errors));
    }

    // --- Fusionar fichas -------------------------------------------------------------------------------

    [Fact]
    public async Task Folding_a_walk_in_into_the_account_that_turned_out_to_be_theirs()
    {
        var store = await NewStore();
        var account = NewAccount();

        // Ana played twice before registering, and once from the web afterwards.
        await Counter(store).RegisterAsync(await NewTournament(store), "Ana Pérez");
        await Counter(store).RegisterAsync(await NewTournament(store), "Ana Pérez");
        await Player().JoinAsync(await NewTournament(store, published: true), account, "Ana Pérez");

        var book = await Book(store).AllAsync();
        var online = book.Single(row => row.HasAccount);
        var counter = book.Single(row => !row.HasAccount);

        Assert.Equal(2, book.Count);

        var merged = await Book(store).MergeAsync(keepId: counter.Id, foldId: online.Id);

        Assert.True(merged.Succeeded, string.Join(" ", merged.Errors));

        var after = Assert.Single(await Book(store).AllAsync());

        // One person, three tournaments, and the account came across with her.
        Assert.Equal(counter.Id, after.Id);
        Assert.Equal(3, after.Tournaments);
        Assert.True(after.HasAccount);
        Assert.Null(after.PossibleDuplicateOf);
    }

    [Fact]
    public async Task After_a_merge_the_counter_stops_offering_the_folded_entry()
    {
        var store = await NewStore();

        // Different tournaments: two entries that played the same one cannot be merged, which is
        // its own test below.
        await Counter(store).RegisterAsync(await NewTournament(store), "Ana");
        await Counter(store).RegisterAsync(await NewTournament(store), "Ana", asSomebodyNew: true);

        var book = await Book(store).AllAsync();

        Assert.True((await Book(store).MergeAsync(book[0].Id, book[1].Id)).Succeeded);

        Assert.Single(await Book(store).SearchAsync("ana"));

        // And signing "Ana" in again lands on the survivor rather than resurrecting the other.
        var later = await Counter(store).RegisterAsync(await NewTournament(store), "Ana");

        Assert.True(later.Succeeded, string.Join(" ", later.Errors));
        Assert.Equal(3, Assert.Single(await Book(store).AllAsync()).Tournaments);
    }

    [Fact]
    public async Task Two_entries_with_their_own_account_are_two_people_and_will_not_merge()
    {
        var store = await NewStore();

        await Player().JoinAsync(await NewTournament(store, published: true), NewAccount(), "Ana Pérez");
        await Player().JoinAsync(await NewTournament(store, published: true), NewAccount(), "Ana Pérez");

        var book = await Book(store).AllAsync();
        var refused = await Book(store).MergeAsync(book[0].Id, book[1].Id);

        Assert.False(refused.Succeeded);
        Assert.Contains("su propia cuenta", string.Join(" ", refused.Errors));
        Assert.Equal(2, (await Book(store).AllAsync()).Count);
    }

    [Fact]
    public async Task Two_entries_that_played_the_same_tournament_will_not_merge_without_a_drop_first()
    {
        var store = await NewStore();
        var tournament = await NewTournament(store);

        await Counter(store).RegisterAsync(tournament, "Ana");
        await Counter(store).RegisterAsync(tournament, "Ana", asSomebodyNew: true);

        var book = await Book(store).AllAsync();

        // Merging them would leave one person twice on that roster, and counted twice in the
        // standings. The store unpicks it first, and the message says which tournament.
        var refused = await Book(store).MergeAsync(book[0].Id, book[1].Id);

        Assert.False(refused.Succeeded);
        Assert.Contains("Torneo de prueba", string.Join(" ", refused.Errors));
        Assert.Equal(2, (await Book(store).AllAsync()).Count);
    }

    [Fact]
    public async Task A_player_from_another_store_cannot_be_merged_into_yours()
    {
        var mine = await NewStore();
        var theirs = await NewStore();

        await Counter(mine).RegisterAsync(await NewTournament(mine), "Ana Pérez");
        await Counter(theirs).RegisterAsync(await NewTournament(theirs), "Ana Pérez");

        var ours = Assert.Single(await Book(mine).AllAsync());
        var other = Assert.Single(await Book(theirs).AllAsync());

        var refused = await Book(mine).MergeAsync(ours.Id, other.Id);

        Assert.False(refused.Succeeded);
        Assert.Contains("no está en tu fichero", string.Join(" ", refused.Errors));
    }

    [Fact]
    public async Task A_folded_entry_cannot_be_folded_again()
    {
        var store = await NewStore();

        await Counter(store).RegisterAsync(await NewTournament(store), "Ana");
        await Counter(store).RegisterAsync(await NewTournament(store), "Ana", asSomebodyNew: true);
        await Counter(store).RegisterAsync(await NewTournament(store), "Ana", asSomebodyNew: true);

        var book = await Book(store).AllAsync();

        Assert.True((await Book(store).MergeAsync(book[0].Id, book[1].Id)).Succeeded);

        var again = await Book(store).MergeAsync(book[2].Id, book[1].Id);

        Assert.False(again.Succeeded);
        Assert.Contains("ya se fusionó", string.Join(" ", again.Errors));
    }

    // --- Harness ---------------------------------------------------------------------------------------

    private IStorePlayers Book(Guid storeId) => fixture.CreateStorePlayers(storeId);

    private ParticipantService Counter(Guid storeId) => new(
        new TournamentRepository(fixture.CreateContextFactory(storeId)),
        fixture.CreateStorePlayers(storeId));

    /// <summary>The player's own path: no store in scope, like a signed-in visitor.</summary>
    private PlayerEntryService Player() => new(
        new TournamentRepository(fixture.CreateContextFactory()),
        fixture.CreateStorePlayers(),
        TimeProvider.System);

    private static string NewAccount() => $"cuenta-{Guid.CreateVersion7():N}";

    private async Task<Guid> NewStore()
    {
        await using var db = fixture.CreateContext();

        var store = new Store(
            $"Tienda {Guid.CreateVersion7():N}",
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"));

        store.Approve();

        db.Stores.Add(store);
        await db.SaveChangesAsync();

        return store.Id;
    }

    private async Task<Guid> NewTournament(Guid storeId, bool published = false)
    {
        await using var db = fixture.CreateContext(storeId);

        await CatalogSeeder.SeedAsync(db);

        var ruleset = await db.Rulesets
            .Include(candidate => candidate.Tcg)
            .SingleAsync(candidate => candidate.Tcg.Code == SeedCatalog.MagicCode);

        var tournament = Tournament.Create(
            storeId,
            ruleset,
            "Torneo de prueba",
            DateTimeOffset.UtcNow.AddDays(7),
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"),
            capacity: 16,
            swissRounds: 3);

        if (published)
        {
            tournament.Publish();
        }

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return tournament.Id;
    }
}
