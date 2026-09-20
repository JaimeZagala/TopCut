using TopCut.Domain.Catalog;

namespace TopCut.Application.Abstractions;

/// <summary>
/// A TCG as the "create tournament" screen needs it: enough to fill the form in before the
/// organiser touches anything.
/// </summary>
/// <param name="AttendanceBands">
/// The game's official table, carried along so the form can re-answer "how many rounds?" as the
/// expected turnout is typed, without a round trip per keystroke.
/// </param>
public sealed record TcgOption(
    int Id,
    string Code,
    string Name,
    PhaseSettings SwissPhase,
    PhaseSettings TopCutPhase,
    TimeExpiryPolicy TimeExpiry,
    IReadOnlyList<AttendanceBand> AttendanceBands)
{
    /// <summary>What this game prescribes for that turnout. A suggestion, never an imposition.</summary>
    public AttendanceBand? RecommendationFor(int players) => AttendanceBand.For(AttendanceBands, players);
}

/// <summary>The games a store can currently create tournaments for.</summary>
public interface ITcgCatalog
{
    Task<IReadOnlyList<TcgOption>> ActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The ruleset a new tournament copies its rules from. <c>null</c> when the game does not exist
    /// or the Superadmin has switched it off, which is the same answer as far as the caller is
    /// concerned: no tournament can be created for it.
    /// </summary>
    Task<Ruleset?> ActiveRulesetForAsync(int tcgId, CancellationToken cancellationToken = default);
}
