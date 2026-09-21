using TopCut.Domain.Catalog;
using TopCut.Domain.Tournaments;
using TopCut.Web.Components.Pages.Store;
using TopCut.Web.Resources;

namespace TopCut.Web.Components.Pages.Public;

/// <summary>
/// One button on the player's own screen: "Gano 2-1", not "2-1".
/// <para>
/// The desk reads every table from player A's side, because it is looking at a list of tables.
/// Somebody sitting at one of them is not: they know who they are, and the fastest tap is the one
/// that says what happened to <b>them</b>. Getting this backwards is how a player reports their own
/// loss as a win.
/// </para>
/// </summary>
/// <param name="Games">
/// Still from player A's side, because that is how the tournament stores a result. The turning
/// around happens here, once, and never in the page.
/// </param>
public sealed record PlayerOutcome(
    string Label,
    IReadOnlyList<GameWinner> Games,
    int MyGames,
    int OpponentGames,
    bool IsDraw)
{
    public bool IWin => !IsDraw && MyGames > OpponentGames;

    /// <summary>
    /// Every result this format allows, worded from the player's side and always in the same order
    /// — best for them at the top — whichever side of the table they sat at. Two players looking at
    /// buttons in mirrored orders is a mis-tap waiting to happen.
    /// </summary>
    public static IReadOnlyList<PlayerOutcome> For(MatchFormat format, bool iAmPlayerA) =>
    [
        .. MatchOutcome.For(format)
            .Select(outcome => FromMySide(outcome, iAmPlayerA))
            .OrderByDescending(outcome => outcome.MyGames)
            .ThenBy(outcome => outcome.OpponentGames)
    ];

    private static PlayerOutcome FromMySide(MatchOutcome outcome, bool iAmPlayerA)
    {
        var a = outcome.Games.Count(game => game == GameWinner.PlayerA);
        var b = outcome.Games.Count(game => game == GameWinner.PlayerB);

        var (mine, theirs) = iAmPlayerA ? (a, b) : (b, a);

        var label = outcome.IsDraw
            ? Textos.MatchDraw
            : string.Format(mine > theirs ? Textos.PlayerWinBy : Textos.PlayerLoseBy, mine, theirs);

        return new PlayerOutcome(label, outcome.Games, mine, theirs, outcome.IsDraw);
    }
}
