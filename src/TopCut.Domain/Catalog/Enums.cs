namespace TopCut.Domain.Catalog;

/// <summary>Number of games in a match. Configured per phase, not per tournament.</summary>
public enum MatchFormat
{
    /// <summary>Single game. The Swiss format in One Piece Card Game.</summary>
    Bo1 = 1,

    /// <summary>Best of three. The standard in Magic and in One Piece top cuts.</summary>
    Bo3 = 3
}

public static class MatchFormatExtensions
{
    /// <summary>Games a player must win to take the match.</summary>
    public static int GamesToWin(this MatchFormat format) => format switch
    {
        MatchFormat.Bo1 => 1,
        MatchFormat.Bo3 => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    /// <summary>Most games the match can possibly run to.</summary>
    public static int MaxGames(this MatchFormat format) => (int)format;
}

/// <summary>
/// Available tiebreakers. Each TCG declares which ones it uses and in what order, because they do
/// not agree: Magic has three levels built on individual games, One Piece only two, since its
/// Swiss phase is played as a single game.
/// </summary>
public enum Tiebreaker
{
    /// <summary>
    /// The player's own match-win percentage. First tiebreaker in One Piece, where byes are left
    /// out of the calculation, so two players on equal points can still differ.
    /// </summary>
    OwnMatchWinPercentage = 1,

    /// <summary>Average match-win percentage of the opponents faced (Magic's OMW%).</summary>
    OpponentsMatchWinPercentage = 2,

    /// <summary>Percentage of individual games the player won (Magic's GW%).</summary>
    GameWinPercentage = 3,

    /// <summary>Average game-win percentage of the opponents faced (Magic's OGW%).</summary>
    OpponentsGameWinPercentage = 4,

    /// <summary>
    /// Result of the match the tied players played against each other, if they played one. One
    /// Piece prescribes it as the last resort before falling back to a random draw.
    /// </summary>
    HeadToHead = 5
}

/// <summary>A tournament phase. Each one carries its own format and timing.</summary>
public enum Phase
{
    Swiss = 1,

    /// <summary>Single-elimination bracket afterwards. Optional.</summary>
    TopCut = 2
}

/// <summary>A match result seen from one of the two players.</summary>
public enum MatchResult
{
    Win = 1,
    Draw = 2,
    Loss = 3,

    /// <summary>
    /// Automatic win from an odd player count. Kept distinct from a normal win because
    /// tiebreakers treat it separately.
    /// </summary>
    Bye = 4
}

/// <summary>
/// What happens to a match that still has no result when the round clock runs out (ADR-0009).
/// </summary>
public enum TimeExpiryPolicy
{
    /// <summary>
    /// Whoever is ahead on games takes the match; a draw only if they are level. This is what both
    /// official rulebooks prescribe once extra turns are done, and the default for both TCGs.
    /// </summary>
    LeaderWins = 1,

    /// <summary>
    /// Always a draw, whatever the game score. Simpler to run, and what the original brief asked
    /// for; available for stores that prefer to close rounds fast and amend by hand.
    /// </summary>
    AlwaysDraw = 2
}
