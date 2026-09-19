using TopCut.Domain.Common;

namespace TopCut.Domain.Catalog;

/// <summary>
/// A TCG's competition rules, held as configuration rather than per-game code. A tournament
/// <b>copies</b> these values when it is created, so changing a ruleset never rewrites the rules of
/// tournaments that already exist (see <c>Tournaments.TournamentSettings</c>).
/// </summary>
public class Ruleset : CatalogEntity
{
    private List<Tiebreaker> _tiebreakers = [];

    private Ruleset() { }

    public Ruleset(
        int tcgId,
        int winPoints,
        int drawPoints,
        int lossPoints,
        int byePoints,
        int gameWinPoints,
        int gameDrawPoints,
        int gameLossPoints,
        bool byesCountInOwnWinPercentage,
        decimal minimumPercentage,
        PhaseSettings swissPhase,
        PhaseSettings topCutPhase,
        IEnumerable<Tiebreaker> tiebreakers,
        TimeExpiryPolicy timeExpiry,
        string rulesVersion,
        string officialSource)
    {
        var ordered = tiebreakers.ToList();

        DomainException.Require(ordered.Count > 0, "Un ruleset necesita al menos un criterio de desempate.");
        DomainException.Require(
            ordered.Distinct().Count() == ordered.Count,
            "Los criterios de desempate no pueden repetirse.");
        DomainException.Require(
            minimumPercentage is >= 0m and <= 1m,
            "El porcentaje minimo debe estar entre 0 y 1.");
        DomainException.Require(
            winPoints >= drawPoints && drawPoints >= lossPoints,
            "Los puntos deben ser decrecientes: victoria >= empate >= derrota.");

        TcgId = tcgId;
        WinPoints = winPoints;
        DrawPoints = drawPoints;
        LossPoints = lossPoints;
        ByePoints = byePoints;
        GameWinPoints = gameWinPoints;
        GameDrawPoints = gameDrawPoints;
        GameLossPoints = gameLossPoints;
        ByesCountInOwnWinPercentage = byesCountInOwnWinPercentage;
        MinimumPercentage = minimumPercentage;
        SwissPhase = swissPhase;
        TopCutPhase = topCutPhase;
        _tiebreakers = ordered;
        TimeExpiry = timeExpiry;
        RulesVersion = rulesVersion;
        OfficialSource = officialSource;
    }

    public int TcgId { get; private set; }

    public Tcg Tcg { get; private set; } = null!;

    // --- Match scoring --------------------------------------------------------------------------

    public int WinPoints { get; private set; }

    public int DrawPoints { get; private set; }

    public int LossPoints { get; private set; }

    /// <summary>Points a bye is worth. In both Magic and One Piece it equals a win.</summary>
    public int ByePoints { get; private set; }

    // --- Individual game scoring (only the game-based tiebreakers read these) --------------------

    public int GameWinPoints { get; private set; }

    public int GameDrawPoints { get; private set; }

    public int GameLossPoints { get; private set; }

    // --- Tiebreaking ------------------------------------------------------------------------------

    /// <summary>
    /// Whether the player's own byes count towards their win percentage. <b>This is where the two
    /// games disagree</b>: Magic includes them (a bye is a 2-0 win for every purpose) and One Piece
    /// excludes them from both numerator and denominator. This flag is why the ruleset is data.
    /// </summary>
    public bool ByesCountInOwnWinPercentage { get; private set; }

    /// <summary>
    /// Floor applied to any percentage before averaging it, so one opponent's bad run does not sink
    /// everyone else's tiebreakers. Both games use 0.33.
    /// </summary>
    public decimal MinimumPercentage { get; private set; }

    /// <summary>Tiebreakers <b>in the order they are applied</b>. The order is significant.</summary>
    public IReadOnlyList<Tiebreaker> Tiebreakers => _tiebreakers.AsReadOnly();

    /// <summary>What happens to unreported matches when the round clock runs out (ADR-0009).</summary>
    public TimeExpiryPolicy TimeExpiry { get; private set; }

    // --- Phases ------------------------------------------------------------------------------------

    public PhaseSettings SwissPhase { get; private set; } = null!;

    public PhaseSettings TopCutPhase { get; private set; } = null!;

    // --- Traceability --------------------------------------------------------------------------------

    /// <summary>Version of the official rulebook these values were checked against.</summary>
    public string RulesVersion { get; private set; } = null!;

    /// <summary>URL of that document, so the values can be re-verified when the body revises them.</summary>
    public string OfficialSource { get; private set; } = null!;

    /// <summary>Points earned for a given match result.</summary>
    public int PointsFor(MatchResult result) => result switch
    {
        MatchResult.Win => WinPoints,
        MatchResult.Draw => DrawPoints,
        MatchResult.Loss => LossPoints,
        MatchResult.Bye => ByePoints,
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };

    /// <summary>Settings for the given phase.</summary>
    public PhaseSettings SettingsFor(Phase phase) => phase switch
    {
        Phase.Swiss => SwissPhase,
        Phase.TopCut => TopCutPhase,
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    /// <summary>
    /// Adjusts a TCG's default ruleset (Superadmin panel). Only affects tournaments created from
    /// now on: existing ones keep the copy they took when they were created.
    /// </summary>
    public void SetPhase(Phase phase, PhaseSettings settings)
    {
        if (phase == Phase.Swiss)
        {
            SwissPhase = settings;
        }
        else
        {
            TopCutPhase = settings;
        }
    }

    /// <summary>Replaces or reorders the TCG's tiebreakers.</summary>
    public void SetTiebreakers(IEnumerable<Tiebreaker> tiebreakers)
    {
        var ordered = tiebreakers.ToList();

        DomainException.Require(ordered.Count > 0, "Un ruleset necesita al menos un criterio de desempate.");
        DomainException.Require(
            ordered.Distinct().Count() == ordered.Count,
            "Los criterios de desempate no pueden repetirse.");

        _tiebreakers = ordered;
    }

    public void SetTimeExpiryPolicy(TimeExpiryPolicy policy) => TimeExpiry = policy;

    /// <summary>Records which official rulebook version these values were last checked against.</summary>
    public void MarkReviewedAgainst(string rulesVersion, string officialSource)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(rulesVersion), "Indica la version del reglamento.");
        DomainException.Require(!string.IsNullOrWhiteSpace(officialSource), "Indica la fuente oficial.");

        RulesVersion = rulesVersion;
        OfficialSource = officialSource;
    }
}
