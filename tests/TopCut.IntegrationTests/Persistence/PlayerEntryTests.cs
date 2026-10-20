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
/// A player signing themselves up from the public sheet, against real PostgreSQL. Every write here
/// lands in a <b>different store's</b> tournament than the one the caller is scoped to (ADR-0031),
/// which is the thing worth testing: the rest of the application cannot do that and must not.
/// </summary>
[Collection(PostgresCollection.Name)]
public class PlayerEntryTests(PostgresFixture fixture)
{
    // --- Signing up -------------------------------------------------------------------------------

    [Fact]
    public async Task A_player_with_no_store_of_their_own_can_sign_up_for_somebody_elses_tournament()
    {
        var ana = NewPlayer();
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        var outcome = await Players().JoinAsync(id, ana, "Ana");

        Assert.True(outcome.Succeeded, string.Join(" ", outcome.Errors));
        Assert.False(outcome.Waitlisted);

        var entry = await Entries().ForAsync(id, ana);

        Assert.NotNull(entry);
        Assert.Equal("Ana", entry!.Name);
        Assert.True(entry.HoldsASeat);

        // And the row really belongs to the organising store, not to nobody.
        await using var db = fixture.CreateContext(store);
        Assert.Equal(store, (await db.Participants.SingleAsync(p => p.Id == entry.ParticipantId)).TenantId);
    }

    [Fact]
    public async Task A_full_tournament_puts_the_player_in_the_queue_and_says_where()
    {
        var ana = NewPlayer();
        var store = await NewStore();
        var id = await NewTournament(store, capacity: 2, published: true);

        await FillWithWalkIns(store, id, 2);

        var outcome = await Players().JoinAsync(id, ana, "Ana");

        Assert.True(outcome.Succeeded, string.Join(" ", outcome.Errors));
        Assert.True(outcome.Waitlisted);
        Assert.Equal(1, outcome.WaitlistPosition);
    }

    [Fact]
    public async Task A_tournament_that_is_not_public_cannot_be_joined_by_guessing_its_address()
    {
        var ana = NewPlayer();
        var store = await NewStore();
        var draft = await NewTournament(store, published: false);
        var hidden = await NewTournament(await NewStore(isTestStore: true), published: true);

        foreach (var id in new[] { draft, hidden, Guid.CreateVersion7() })
        {
            var outcome = await Players().JoinAsync(id, ana, "Ana");

            Assert.False(outcome.Succeeded);
            Assert.Contains("No existe ese torneo", string.Join(" ", outcome.Errors));
        }
    }

    [Fact]
    public async Task Signing_up_twice_is_refused()
    {
        var ana = NewPlayer();
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        await Players().JoinAsync(id, ana, "Ana");
        var again = await Players().JoinAsync(id, ana, "Ana");

        Assert.False(again.Succeeded);
        Assert.Contains("ya está inscrito", string.Join(" ", again.Errors));
    }

    // --- Withdrawing ------------------------------------------------------------------------------

    [Fact]
    public async Task Withdrawing_frees_the_seat_and_the_first_person_waiting_moves_up()
    {
        var (ana, bruno) = (NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, capacity: 2, published: true);

        await FillWithWalkIns(store, id, 1);
        await Players().JoinAsync(id, ana, "Ana");
        await Players().JoinAsync(id, bruno, "Bruno");

        Assert.True((await Entries().ForAsync(id, bruno))!.IsWaiting);

        var left = await Players().WithdrawAsync(id, ana);

        Assert.True(left.Succeeded, string.Join(" ", left.Errors));
        Assert.True((await Entries().ForAsync(id, ana))!.HasWithdrawn);
        Assert.True((await Entries().ForAsync(id, bruno))!.HoldsASeat);
    }

    [Fact]
    public async Task Coming_back_after_withdrawing_keeps_one_row_and_joins_the_queue()
    {
        var (ana, bruno) = (NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, capacity: 2, published: true);

        await FillWithWalkIns(store, id, 1);
        await Players().JoinAsync(id, ana, "Ana");
        await Players().WithdrawAsync(id, ana);
        await Players().JoinAsync(id, bruno, "Bruno");

        var back = await Players().JoinAsync(id, ana, "Ana");

        Assert.True(back.Succeeded, string.Join(" ", back.Errors));
        Assert.True(back.Waitlisted);

        await using var db = fixture.CreateContext(store);
        var rows = await db.Participants.Where(p => p.TournamentId == id).ToListAsync();

        // One line per person: two would double-count Ana on the roster.
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task Once_the_tournament_starts_the_player_is_sent_to_the_store_instead()
    {
        var ana = NewPlayer();
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        await Players().JoinAsync(id, ana, "Ana");
        await FillWithWalkIns(store, id, 3);
        await StartFirstRound(store, id);

        var refused = await Players().WithdrawAsync(id, ana);

        Assert.False(refused.Succeeded);
        Assert.Contains("Habla con la tienda", string.Join(" ", refused.Errors));
        Assert.True((await Entries().ForAsync(id, ana))!.HoldsASeat);
    }

    [Fact]
    public async Task Withdrawing_from_a_tournament_you_never_joined_is_refused()
    {
        var ana = NewPlayer();
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        var refused = await Players().WithdrawAsync(id, ana);

        Assert.False(refused.Succeeded);
        Assert.Contains("No estás inscrito", string.Join(" ", refused.Errors));
    }

    // --- The last seat ------------------------------------------------------------------------------

    [Fact]
    public async Task Two_players_going_for_the_last_seat_at_the_same_time_do_not_both_get_it()
    {
        var (ana, bruno) = (NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, capacity: 2, published: true);

        await FillWithWalkIns(store, id, 1);

        var ahead = new TournamentRepository(fixture.CreateContextFactory(store));
        var behind = new TournamentRepository(fixture.CreateContextFactory(store));

        OperationResult? theirs = null;

        // Bruno loads, signs up and saves while Ana still has the aggregate in her hands, which is
        // exactly what two phones a second apart look like from the database's side.
        var mine = await ahead.ExecuteOnPublicAsync(id, tournament =>
        {
            tournament.SignUp("Ana", ana);

            theirs = behind
                .ExecuteOnPublicAsync(id, other =>
                {
                    other.SignUp("Bruno", bruno);
                    return OperationResult.Ok();
                })
                .GetAwaiter()
                .GetResult();

            return OperationResult.Ok();
        });

        Assert.True(theirs!.Succeeded, string.Join(" ", theirs.Errors));

        // Ana's save carries a row version that is no longer current. Before the tournament row was
        // made part of every write to its aggregate, both inserts went through and the tournament
        // ended up with two players in one seat.
        Assert.False(mine.Succeeded);
        Assert.Contains("Recarga y vuelve a intentarlo", string.Join(" ", mine.Errors));

        await using var db = fixture.CreateContext(store);
        var seated = await db.Participants
            .CountAsync(p => p.TournamentId == id && p.Status == ParticipantStatus.Registered);

        Assert.Equal(2, seated);
    }

    // --- Mis torneos ---------------------------------------------------------------------------------

    [Fact]
    public async Task The_players_own_list_spans_every_store_they_have_played_at()
    {
        var ana = NewPlayer();
        var oneShop = await NewStore();
        var another = await NewStore();
        var now = DateTimeOffset.UtcNow;

        var soon = await NewTournament(oneShop, name: "Este sábado", startsAt: now.AddDays(2), published: true);
        var later = await NewTournament(another, name: "El mes que viene", startsAt: now.AddDays(30), published: true);
        var played = await NewTournament(another, name: "El mes pasado", startsAt: now.AddDays(-30), published: true);
        await NewTournament(oneShop, name: "Sin mi", published: true);

        foreach (var id in new[] { soon, later, played })
        {
            Assert.True((await Players().JoinAsync(id, ana, "Ana")).Succeeded);
        }

        var upcoming = await Entries().HistoryAsync(ana);

        Assert.Equal(["Este sábado", "El mes que viene"], upcoming.Select(row => row.Name));
        Assert.Equal([ParticipantStatus.Registered, ParticipantStatus.Registered], upcoming.Select(row => row.Status));

        var past = await Entries().HistoryAsync(ana, past: true);

        Assert.Equal("El mes pasado", Assert.Single(past).Name);
    }

    [Fact]
    public async Task A_tournament_the_player_withdrew_from_drops_off_their_list()
    {
        var ana = NewPlayer();
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        await Players().JoinAsync(id, ana, "Ana");
        Assert.Single(await Entries().HistoryAsync(ana));

        await Players().WithdrawAsync(id, ana);

        // "Mis torneos" is where the player is going, not a record of everything they reconsidered.
        Assert.Empty(await Entries().HistoryAsync(ana));
    }

    // --- La mesa del jugador -------------------------------------------------------------------------

    [Fact]
    public async Task The_player_sees_their_own_table_read_from_their_own_side()
    {
        var (ana, bruno) = (NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        await Players().JoinAsync(id, ana, "Ana");
        await Players().JoinAsync(id, bruno, "Bruno");
        await StartFirstRound(store, id);

        var hers = await Players().BoardAsync(id, ana);
        var his = await Players().BoardAsync(id, bruno);

        Assert.NotNull(hers?.Match);
        Assert.NotNull(his?.Match);

        // Same table, and each of them is the other's opponent. Getting this backwards is how a
        // player reports their own loss as a win.
        Assert.Equal(hers!.Match!.Table, his!.Match!.Table);
        Assert.Equal("Bruno", hers.Match.OpponentName);
        Assert.Equal("Ana", his.Match.OpponentName);
        Assert.NotEqual(hers.Match.IAmPlayerA, his.Match.IAmPlayerA);
        Assert.True(hers.Match.CanReport);
    }

    [Fact]
    public async Task Player_B_reporting_a_win_is_stored_as_a_win_for_player_B()
    {
        var (ana, bruno) = (NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        await Players().JoinAsync(id, ana, "Ana");
        await Players().JoinAsync(id, bruno, "Bruno");
        await StartFirstRound(store, id);

        // Bruno reports winning 2-0 from his own side, whichever side the pairer sat him on.
        var seated = await Players().BoardAsync(id, bruno);
        var match = seated!.Match!;
        var mine = match.IAmPlayerA ? GameWinner.PlayerA : GameWinner.PlayerB;

        var reported = await Players().ReportAsync(id, bruno, match.RoundId, match.PairingId, [mine, mine]);

        Assert.True(reported.Succeeded, string.Join(" ", reported.Errors));

        var his = (await Players().BoardAsync(id, bruno))!.Match!;
        var hers = (await Players().BoardAsync(id, ana))!.Match!;

        Assert.Equal("2-0", his.Score);
        Assert.True(his.IWon);
        Assert.True(his.ReportedByMe);

        // The same result, mirrored, on the other phone.
        Assert.Equal("0-2", hers.Score);
        Assert.False(hers.IWon);
        Assert.False(hers.ReportedByMe);
    }

    [Fact]
    public async Task A_result_can_only_be_reported_once_and_the_rest_is_for_the_desk()
    {
        var (ana, bruno) = (NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        await Players().JoinAsync(id, ana, "Ana");
        await Players().JoinAsync(id, bruno, "Bruno");
        await StartFirstRound(store, id);

        var match = (await Players().BoardAsync(id, ana))!.Match!;
        var hers = match.IAmPlayerA ? GameWinner.PlayerA : GameWinner.PlayerB;

        Assert.True((await Players().ReportAsync(id, ana, match.RoundId, match.PairingId, [hers, hers])).Succeeded);

        // Bruno disagrees. Two phones correcting each other is how a table ends up with a score
        // neither player agreed to, so the second one is refused and sent to the desk.
        var his = match.IAmPlayerA ? GameWinner.PlayerB : GameWinner.PlayerA;
        var refused = await Players().ReportAsync(id, bruno, match.RoundId, match.PairingId, [his, his]);

        Assert.False(refused.Succeeded);
        Assert.Contains("selo a la tienda", string.Join(" ", refused.Errors));
        Assert.Equal("2-0", (await Players().BoardAsync(id, ana))!.Match!.Score);
    }

    [Fact]
    public async Task Nobody_can_report_a_table_that_is_not_theirs()
    {
        var (ana, bruno, carla) = (NewPlayer(), NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        foreach (var (account, name) in new[] { (ana, "Ana"), (bruno, "Bruno"), (carla, "Carla") })
        {
            await Players().JoinAsync(id, account, name);
        }

        await FillWithWalkIns(store, id, 1);
        await StartFirstRound(store, id);

        var mine = (await Players().BoardAsync(id, ana))!.Match!;

        // Whoever the pairer did not sit at Ana's table. Asked rather than assumed: with four
        // players there are two tables, but which pair is which is the pairer's business.
        PlayerMatch? theirs = null;

        foreach (var account in new[] { bruno, carla })
        {
            var match = (await Players().BoardAsync(id, account))!.Match!;

            if (match.PairingId != mine.PairingId)
            {
                theirs = match;
                break;
            }
        }

        Assert.NotNull(theirs);

        // The pairing id travels in a form, so somebody could post another table's.
        var refused = await Players().ReportAsync(
            id, ana, theirs!.RoundId, theirs.PairingId, [GameWinner.PlayerA, GameWinner.PlayerA]);

        Assert.False(refused.Succeeded);
        Assert.Contains("no es la tuya", string.Join(" ", refused.Errors));
    }

    [Fact]
    public async Task A_bye_needs_no_reporting_and_arrives_already_won()
    {
        var (ana, bruno, carla) = (NewPlayer(), NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        foreach (var (account, name) in new[] { (ana, "Ana"), (bruno, "Bruno"), (carla, "Carla") })
        {
            await Players().JoinAsync(id, account, name);
        }

        await StartFirstRound(store, id);

        var byes = new List<PlayerMatch>();

        foreach (var account in new[] { ana, bruno, carla })
        {
            var match = (await Players().BoardAsync(id, account))!.Match!;

            if (match.IsBye)
            {
                byes.Add(match);
            }
        }

        // Three players, one bye, and it is born resolved.
        var bye = Assert.Single(byes);

        Assert.False(bye.CanReport);
        Assert.True(bye.Reported);
        Assert.True(bye.IWon);
    }

    [Fact]
    public async Task Between_rounds_the_player_gets_their_standing_and_no_table()
    {
        var (ana, bruno) = (NewPlayer(), NewPlayer());
        var store = await NewStore();
        var id = await NewTournament(store, published: true);

        await Players().JoinAsync(id, ana, "Ana");
        await Players().JoinAsync(id, bruno, "Bruno");
        await StartFirstRound(store, id);

        var match = (await Players().BoardAsync(id, ana))!.Match!;
        var hers = match.IAmPlayerA ? GameWinner.PlayerA : GameWinner.PlayerB;
        await Players().ReportAsync(id, ana, match.RoundId, match.PairingId, [hers, hers]);

        await CloseRound(store, id, match.RoundId);

        var board = (await Players().BoardAsync(id, ana))!;

        Assert.Null(board.Match);
        Assert.Equal(1, board.Rank);
        Assert.Equal(3, board.MatchPoints);
        Assert.Equal("1-0-0", board.Record);
    }

    [Fact]
    public async Task Somebody_who_is_not_in_the_tournament_has_no_board()
    {
        var store = await NewStore();
        var published = await NewTournament(store, published: true);
        var draft = await NewTournament(store, published: false);

        Assert.Null(await Players().BoardAsync(published, NewPlayer()));

        // And a draft answers the same, whoever asks: from outside, "not yours" and "does not
        // exist" have to read alike.
        Assert.Null(await Players().BoardAsync(draft, NewPlayer()));
    }

    // --- Harness ---------------------------------------------------------------------------------------

    /// <summary>
    /// The player's side, with <b>no store in scope</b>: that is what a signed-in player browsing
    /// the public sheet looks like, and what the tenant filter would otherwise close down.
    /// </summary>
    private PlayerEntryService Players() => new(
        new TournamentRepository(fixture.CreateContextFactory()),
        fixture.CreateStorePlayers(),
        TimeProvider.System);

    /// <summary>
    /// An account id nobody else's test is using. The suite shares one database and a player's
    /// history is looked up by account, so a fixed id would pick up other tests' sign-ups.
    /// </summary>
    private static string NewPlayer() => $"cuenta-{Guid.CreateVersion7():N}";

    private IPlayerEntries Entries() => new PlayerEntries(fixture.CreateContextFactory(), TimeProvider.System);

    private async Task<Guid> NewStore(bool isTestStore = false)
    {
        await using var db = fixture.CreateContext();

        var store = new Store(
            $"Tienda {Guid.CreateVersion7():N}",
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"),
            isTestStore);

        store.Approve();

        db.Stores.Add(store);
        await db.SaveChangesAsync();

        return store.Id;
    }

    private async Task<Guid> NewTournament(
        Guid storeId,
        string name = "Torneo de prueba",
        DateTimeOffset? startsAt = null,
        int capacity = 16,
        bool published = false)
    {
        await using var db = fixture.CreateContext(storeId);

        await CatalogSeeder.SeedAsync(db);

        var ruleset = await db.Rulesets
            .Include(candidate => candidate.Tcg)
            .SingleAsync(candidate => candidate.Tcg.Code == SeedCatalog.MagicCode);

        var tournament = Tournament.Create(
            storeId,
            ruleset,
            name,
            startsAt ?? DateTimeOffset.UtcNow.AddDays(7),
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"),
            capacity,
            swissRounds: 3);

        if (published)
        {
            tournament.Publish();
        }

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return tournament.Id;
    }

    /// <summary>Walk-ins typed in by the store, to take the seats up without involving an account.</summary>
    private Task FillWithWalkIns(Guid storeId, Guid tournamentId, int players) =>
        AsStore(storeId, tournamentId, tournament =>
        {
            for (var seat = 1; seat <= players; seat++)
            {
                tournament.SignUp($"De la calle {seat}");
            }
        });

    private Task StartFirstRound(Guid storeId, Guid tournamentId) =>
        AsStore(storeId, tournamentId, tournament =>
            TournamentEngine.StartNextRound(tournament, DateTimeOffset.UtcNow));

    private Task CloseRound(Guid storeId, Guid tournamentId, Guid roundId) =>
        AsStore(storeId, tournamentId, tournament => tournament.CloseRound(roundId, DateTimeOffset.UtcNow));

    private async Task AsStore(Guid storeId, Guid tournamentId, Action<Tournament> change)
    {
        var repository = new TournamentRepository(fixture.CreateContextFactory(storeId));

        var result = await repository.ExecuteAsync(tournamentId, tournament =>
        {
            change(tournament);
            return OperationResult.Ok();
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }
}
