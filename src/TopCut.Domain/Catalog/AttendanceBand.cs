namespace TopCut.Domain.Catalog;

/// <summary>
/// One row of a TCG's attendance table: how many Swiss rounds to play for a given turnout, and how
/// many players the cut should take.
/// <para>
/// The number of rounds is chosen so that, at best, exactly one player finishes undefeated. Too few
/// and the winner is decided by tiebreakers instead of by playing; too many and the room is there
/// all night for nothing.
/// </para>
/// </summary>
/// <param name="UpToPlayers">Inclusive upper bound of the band.</param>
/// <param name="TopCutSize">Players the bracket takes, or <c>null</c> when the TCG suggests none.</param>
public readonly record struct AttendanceBand(int UpToPlayers, int SwissRounds, int? TopCutSize)
{
    /// <summary>
    /// The band that applies to a turnout, given a table ordered by <see cref="UpToPlayers"/>.
    /// <para>
    /// Lives here rather than only on <c>Ruleset</c> so that the screen where the store types an
    /// expected turnout can answer the same question without loading the aggregate.
    /// </para>
    /// </summary>
    public static AttendanceBand? For(IEnumerable<AttendanceBand> bands, int players) =>
        bands.FirstOrDefault(band => players <= band.UpToPlayers) is { UpToPlayers: > 0 } match
            ? match
            : null;
}
