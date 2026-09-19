using TopCut.Domain.Catalog;
using TopCut.Domain.Common;

namespace TopCut.Domain.Tournaments;

/// <summary>
/// The rules actually in force for one tournament. It is a <b>copy</b> of the TCG's
/// <see cref="Ruleset"/> taken when the tournament was created: if the Superadmin later corrects
/// the default ruleset, tournaments that already exist do not change retroactively (an explicit
/// requirement in docs/vision.md).
/// </summary>
public class TournamentSettings
{
    private List<Tiebreaker> _tiebreakers = [];

    private TournamentSettings() { }

    public static TournamentSettings CopyFrom(Ruleset ruleset) => new()
    {
        SourceRulesetId = ruleset.Id,
        RulesVersion = ruleset.RulesVersion,
        WinPoints = ruleset.WinPoints,
        DrawPoints = ruleset.DrawPoints,
        LossPoints = ruleset.LossPoints,
        ByePoints = ruleset.ByePoints,
        GameWinPoints = ruleset.GameWinPoints,
        GameDrawPoints = ruleset.GameDrawPoints,
        GameLossPoints = ruleset.GameLossPoints,
        ByesCountInOwnWinPercentage = ruleset.ByesCountInOwnWinPercentage,
        MinimumPercentage = ruleset.MinimumPercentage,
        SwissPhase = ruleset.SwissPhase,
        TopCutPhase = ruleset.TopCutPhase,
        TimeExpiry = ruleset.TimeExpiry,
        _tiebreakers = [.. ruleset.Tiebreakers]
    };

    /// <summary>The ruleset this was copied from, for traceability only. Never read live.</summary>
    public int SourceRulesetId { get; private set; }

    public string RulesVersion { get; private set; } = null!;

    public int WinPoints { get; private set; }

    public int DrawPoints { get; private set; }

    public int LossPoints { get; private set; }

    public int ByePoints { get; private set; }

    public int GameWinPoints { get; private set; }

    public int GameDrawPoints { get; private set; }

    public int GameLossPoints { get; private set; }

    public bool ByesCountInOwnWinPercentage { get; private set; }

    public decimal MinimumPercentage { get; private set; }

    public PhaseSettings SwissPhase { get; private set; }

    public PhaseSettings TopCutPhase { get; private set; }

    public TimeExpiryPolicy TimeExpiry { get; private set; }

    public IReadOnlyList<Tiebreaker> Tiebreakers => _tiebreakers.AsReadOnly();

    public int PointsFor(MatchResult result) => result switch
    {
        MatchResult.Win => WinPoints,
        MatchResult.Draw => DrawPoints,
        MatchResult.Loss => LossPoints,
        MatchResult.Bye => ByePoints,
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };

    public PhaseSettings SettingsFor(Phase phase) => phase switch
    {
        Phase.Swiss => SwissPhase,
        Phase.TopCut => TopCutPhase,
        _ => throw new ArgumentOutOfRangeException(nameof(phase))
    };

    /// <summary>Changes a phase's format or timing. Only before the first round starts.</summary>
    internal void SetPhase(Phase phase, PhaseSettings settings)
    {
        DomainException.Require(
            settings.RoundLength is null or { TotalMinutes: > 0 },
            "La duracion de ronda debe ser positiva o nula (sin limite).");

        if (phase == Phase.Swiss)
        {
            SwissPhase = settings;
        }
        else
        {
            TopCutPhase = settings;
        }
    }

    internal void SetTimeExpiryPolicy(TimeExpiryPolicy policy) => TimeExpiry = policy;
}
