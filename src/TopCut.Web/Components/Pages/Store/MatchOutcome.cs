using TopCut.Domain.Catalog;
using TopCut.Domain.Tournaments;
using TopCut.Web.Resources;

namespace TopCut.Web.Components.Pages.Store;

/// <summary>
/// One button on the desk: a label and the sequence of games it stands for.
/// <para>
/// The organiser never types a game-by-game list; they see the score the players tell them and
/// press it. Turning "2-1" into the three games behind it happens here, once, instead of being
/// reinvented every time somebody reports a match.
/// </para>
/// </summary>
/// <param name="Games">
/// The order matters: the domain rejects a match with games played after it was already decided,
/// so a 2-1 has to be win, loss, win and not win, win, loss.
/// </param>
public sealed record MatchOutcome(string Label, IReadOnlyList<GameWinner> Games, bool IsDraw = false)
{
    private static readonly MatchOutcome[] BestOfOne =
    [
        new("1-0", [GameWinner.PlayerA]),
        new(Textos.MatchDraw, [GameWinner.Draw], IsDraw: true),
        new("0-1", [GameWinner.PlayerB])
    ];

    private static readonly MatchOutcome[] BestOfThree =
    [
        new("2-0", [GameWinner.PlayerA, GameWinner.PlayerA]),
        new("2-1", [GameWinner.PlayerA, GameWinner.PlayerB, GameWinner.PlayerA]),
        new(Textos.MatchDraw, [GameWinner.PlayerA, GameWinner.PlayerB, GameWinner.Draw], IsDraw: true),
        new("1-2", [GameWinner.PlayerB, GameWinner.PlayerA, GameWinner.PlayerB]),
        new("0-2", [GameWinner.PlayerB, GameWinner.PlayerB])
    ];

    /// <summary>Every result a match in this format can end in, read from player A's side.</summary>
    public static IReadOnlyList<MatchOutcome> For(MatchFormat format) =>
        format == MatchFormat.Bo1 ? BestOfOne : BestOfThree;
}
