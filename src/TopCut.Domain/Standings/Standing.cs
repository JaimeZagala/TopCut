namespace TopCut.Domain.Standings;

/// <summary>
/// One row of the standings. Carries every tiebreaker value, not just the ones this TCG happens to
/// use, so the live screen can show the organiser why two players are in the order they are in.
/// </summary>
/// <param name="Rank">1-based position once every configured tiebreaker has been applied.</param>
/// <param name="MatchPoints">The primary sort. Everything else only separates players level on this.</param>
/// <param name="Byes">Counted apart from wins because the rulebooks treat them differently.</param>
/// <param name="OwnMatchWinPercentage">
/// Match points over the maximum obtainable, floored at the ruleset's minimum. Whether byes count
/// towards it depends on the TCG: Magic includes them, One Piece does not.
/// </param>
/// <param name="OpponentsMatchWinPercentage">
/// Average of the opponents' own percentages. Rounds where this player had a bye are skipped, since
/// there was no opponent.
/// </param>
/// <param name="Dropped">Still listed: a withdrawal keeps the results it already earned.</param>
public sealed record Standing(
    Guid ParticipantId,
    string Name,
    int Rank,
    int MatchPoints,
    int Wins,
    int Losses,
    int Draws,
    int Byes,
    decimal OwnMatchWinPercentage,
    decimal OpponentsMatchWinPercentage,
    decimal GameWinPercentage,
    decimal OpponentsGameWinPercentage,
    bool Dropped)
{
    /// <summary>Rounds that actually count towards this player's record, byes included.</summary>
    public int RoundsPlayed => Wins + Losses + Draws + Byes;

    /// <summary>The classic W-L-D line, with byes folded into wins as players read them.</summary>
    public string Record => $"{Wins + Byes}-{Losses}-{Draws}";
}
