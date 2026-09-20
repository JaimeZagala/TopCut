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
public readonly record struct AttendanceBand(int UpToPlayers, int SwissRounds, int? TopCutSize);
