namespace TopCut.Domain.Catalog;

/// <summary>
/// Settings for one phase of a tournament (Swiss or top cut). The two phases are independent: a
/// store can run Bo3 Swiss with a Bo1 top cut if that is what fits the room.
/// </summary>
/// <param name="Format">Bo1 or Bo3.</param>
/// <param name="RoundLength">Round clock. <c>null</c> means untimed (championship finals).</param>
/// <param name="ExtraTime">
/// Overtime once the clock runs out, for extra turns. Zero in Magic, which does not treat it as
/// clock time; five or ten minutes in One Piece depending on the phase.
/// </param>
public readonly record struct PhaseSettings(
    MatchFormat Format,
    TimeSpan? RoundLength,
    TimeSpan ExtraTime)
{
    /// <summary>Total clock for the round, overtime included.</summary>
    public TimeSpan? TotalLength => RoundLength is null ? null : RoundLength + ExtraTime;
}
