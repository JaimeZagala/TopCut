using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests.Engine;

/// <summary>
/// The cumulative table of a league (ADR-0037): the same scoring model as one tournament, added up
/// across the season. What has to hold is that a person is one row however many events they played,
/// and that two players level on points are separated the same way every time the page is read.
/// </summary>
public class LeagueTableTests
{
    private static readonly DateTimeOffset January = new(2027, 1, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Points_from_every_tournament_land_on_the_same_row()
    {
        var (ana, bea) = (Entry(), Entry());
        var field = Field((ana, "Ana"), (bea, "Bea"));

        var january = OneRound(field, January, winner: 1);
        var february = OneRound(field, January.AddMonths(1), winner: 1);

        var table = LeagueTable.For([january.Tournament, february.Tournament]);

        Assert.Equal(2, table.Count);

        var first = table[0];

        Assert.Equal(ana, first.StorePlayerId);
        Assert.Equal(1, first.Rank);
        Assert.Equal(6, first.Points);
        Assert.Equal(2, first.Tournaments);
        Assert.Equal("2-0-0", first.Record);

        Assert.Equal(bea, table[1].StorePlayerId);
        Assert.Equal(0, table[1].Points);
        Assert.Equal(2, table[1].Rank);
    }

    [Fact]
    public void A_cancelled_tournament_scores_for_nobody()
    {
        var (ana, bea) = (Entry(), Entry());
        var field = Field((ana, "Ana"), (bea, "Bea"));

        var january = OneRound(field, January, winner: 1);
        var february = OneRound(field, January.AddMonths(1), winner: 1);

        // Called off after it was played: the results are still in the database, and they still
        // score nowhere. Same rule the tournament applies when the store tries to assign one.
        february.Tournament.Cancel();

        var top = LeagueTable.For([january.Tournament, february.Tournament])[0];

        Assert.Equal(3, top.Points);
        Assert.Equal(1, top.Tournaments);
    }

    [Fact]
    public void The_name_shown_is_the_last_one_that_player_signed_up_with()
    {
        var ana = Entry();

        var january = OneRound(Field((ana, "ana perez"), (Entry(), "Bea")), January, winner: 1);
        var march = OneRound(Field((ana, "Ana Pérez"), (Entry(), "Cris")), January.AddMonths(2), winner: 1);

        // Handed over newest first on purpose: the order of the season comes from the dates, not
        // from the order somebody happened to read the rows out of the database.
        var table = LeagueTable.For([march.Tournament, january.Tournament]);

        Assert.Equal("Ana Pérez", table[0].Name);
    }

    [Fact]
    public void Signing_up_for_something_nobody_played_is_not_a_tournament_played()
    {
        var (ana, bea) = (Entry(), Entry());

        var january = OneRound(Field((ana, "Ana"), (bea, "Bea")), January, winner: 1);

        // Published, people on the list, not a single round opened. Nobody has played it.
        var announced = TournamentDriver.Leg(
            Field((ana, "Ana"), (Entry(), "Dani")),
            swissRounds: 1,
            January.AddMonths(1));

        var table = LeagueTable.For([january.Tournament, announced.Tournament]);

        Assert.Equal(2, table.Count);
        Assert.Equal(1, table[0].Tournaments);
        Assert.Equal(1, table[1].Tournaments);

        // And somebody whose only entry is that one is not in the season at all yet.
        Assert.DoesNotContain(table, row => row.Name == "Dani");
    }

    [Fact]
    public void Level_on_points_the_one_who_won_more_matches_goes_above()
    {
        var (ana, bea, cris, dani) = (Entry(), Entry(), Entry(), Entry());
        var field = Field((ana, "Ana"), (bea, "Bea"), (cris, "Cris"), (dani, "Dani"));

        // Ana wins three, Bea draws three: level on nine points once Bea adds two wins, and the
        // whole point of the criterion is that three wins is the better season.
        var january = TournamentDriver.Leg(field, swissRounds: 3, January);

        Play(january, (1, 3), draw: (2, 4));
        Play(january, (1, 4), draw: (2, 3));
        Play(january, (1, 4), draw: (2, 3));

        var february = TournamentDriver.Leg(field, swissRounds: 2, January.AddMonths(1));

        Play(february, (2, 3), (4, 1));
        Play(february, (2, 4), (3, 1));

        var table = LeagueTable.For([january.Tournament, february.Tournament]);

        Assert.Equal(9, table[0].Points);
        Assert.Equal(9, table[1].Points);

        // Same points, same number of tournaments: only the wins separate them.
        Assert.Equal(2, table[0].Tournaments);
        Assert.Equal(2, table[1].Tournaments);
        Assert.Equal("Ana", table[0].Name);
        Assert.Equal("3-2-0", table[0].Record);
        Assert.Equal("Bea", table[1].Name);
        Assert.Equal("2-0-3", table[1].Record);
    }

    [Fact]
    public void Level_on_points_and_on_wins_the_one_who_needed_fewer_tournaments_goes_above()
    {
        var (ana, bea, cris, dani) = (Entry(), Entry(), Entry(), Entry());

        var january = TournamentDriver.Leg(
            Field((ana, "Ana"), (bea, "Bea"), (cris, "Cris"), (dani, "Dani")),
            swissRounds: 2,
            January);

        Play(january, (1, 3), (2, 4));
        Play(january, (1, 4), (3, 2));

        // Ana is not in this one. Bea gets to the same six points, but over two Saturdays.
        var february = TournamentDriver.Leg(
            Field((bea, "Bea"), (cris, "Cris")),
            swissRounds: 1,
            January.AddMonths(1));

        Play(february, (1, 2));

        var table = LeagueTable.For([january.Tournament, february.Tournament]);

        Assert.Equal(6, table[0].Points);
        Assert.Equal(6, table[1].Points);
        Assert.Equal("Ana", table[0].Name);
        Assert.Equal(1, table[0].Tournaments);
        Assert.Equal("Bea", table[1].Name);
        Assert.Equal(2, table[1].Tournaments);
    }

    private static Guid Entry() => Guid.CreateVersion7();

    private static IReadOnlyList<(Guid? Book, string Name)> Field(params (Guid Book, string Name)[] players) =>
        [.. players.Select(player => ((Guid?)player.Book, player.Name))];

    /// <summary>One round with the pairings the test chose, reported and closed.</summary>
    private static void Play(
        TournamentDriver leg,
        (int Winner, int Loser) match,
        (int Winner, int Loser)? second = null,
        (int A, int B)? draw = null)
    {
        var pairs = new List<(int, int)> { match };

        if (second is { } other)
        {
            pairs.Add(other);
        }

        if (draw is { } drawn)
        {
            pairs.Add(drawn);
        }

        var round = leg.StartRoundWith([.. pairs]);

        leg.ReportMatch(round, match.Winner, match.Loser);

        if (second is { } decided)
        {
            leg.ReportMatch(round, decided.Winner, decided.Loser);
        }

        if (draw is { } level)
        {
            leg.ReportDraw(round, level.A, level.B);
        }

        leg.CloseRound(round);
    }

    private static TournamentDriver OneRound(
        IReadOnlyList<(Guid? Book, string Name)> field,
        DateTimeOffset startsAt,
        int winner)
    {
        var leg = TournamentDriver.Leg(field, swissRounds: 1, startsAt);

        Play(leg, (winner, winner == 1 ? 2 : 1));

        return leg;
    }
}
