namespace TopCut.Domain.Catalog;

/// <summary>
/// The v1 TCG catalog and its rulesets, as <b>data</b>. No tournament rule ever asks which game it
/// is looking at: switching on a third TCG means adding an entry here (or creating one from the
/// Superadmin panel), not writing new classes.
/// <para>
/// The values are verified against the official rulebooks cited in <see cref="Ruleset.OfficialSource"/>.
/// Those bodies revise them from time to time: when they do, fix the values here and bump
/// <see cref="Ruleset.RulesVersion"/>.
/// </para>
/// </summary>
public static class SeedCatalog
{
    public const string MagicCode = "MTG";
    public const string OnePieceCode = "OPTCG";

    /// <summary>Floor both games apply to percentages before averaging them.</summary>
    private const decimal PercentageFloor = 0.33m;

    public static Tcg Magic() => new(MagicCode, "Magic: The Gathering");

    public static Tcg OnePiece() => new(OnePieceCode, "One Piece Card Game");

    /// <summary>
    /// Magic: The Gathering, per the current Magic Tournament Rules.
    /// <list type="bullet">
    /// <item>MTR 3.1: tiebreakers in the order OMW%, GW%, OGW%.</item>
    /// <item>MTR appendix B: 50 minutes per round in Constructed and Limited; 90 in quarter- and
    /// semi-finals.</item>
    /// <item>MTR appendix C: 3/1/0 points; a bye counts as a 2-0 win (3 match points, 6 game
    /// points) and <b>does</b> count towards the player's own percentage.</item>
    /// </list>
    /// </summary>
    public static Ruleset MagicRuleset(int tcgId) => new(
        tcgId: tcgId,
        winPoints: 3,
        drawPoints: 1,
        lossPoints: 0,
        byePoints: 3,
        gameWinPoints: 3,
        gameDrawPoints: 1,
        gameLossPoints: 0,
        byesCountInOwnWinPercentage: true,
        minimumPercentage: PercentageFloor,
        swissPhase: new PhaseSettings(MatchFormat.Bo3, TimeSpan.FromMinutes(50), TimeSpan.Zero),
        topCutPhase: new PhaseSettings(MatchFormat.Bo3, TimeSpan.FromMinutes(90), TimeSpan.Zero),
        tiebreakers:
        [
            Tiebreaker.OpponentsMatchWinPercentage,
            Tiebreaker.GameWinPercentage,
            Tiebreaker.OpponentsGameWinPercentage
        ],
        timeExpiry: TimeExpiryPolicy.LeaderWins,
        rulesVersion: "Magic Tournament Rules, 21 abr 2025",
        officialSource: "https://media.wizards.com/ContentResources/WPN/MTG_MTR_2025_Apr%2021_EN.pdf");

    /// <summary>
    /// One Piece Card Game, per the current Bandai Organized Play Tournament Rules Manual.
    /// <list type="bullet">
    /// <item>TRM 3.5: Swiss is a single game (30 min + 5 of overtime) and the top cut is best of
    /// three (60 min + 10). The championship final is untimed.</item>
    /// <item>TRM 5.4: 3 points for a win, 3 for a bye, 1 for a draw, 0 for a loss. Tiebreak on the
    /// player's own match win rate, then their opponents', with a 0.33 floor.</item>
    /// <item>TRM 5.4: byes are excluded from the player's own percentage, <b>unlike Magic</b>. That
    /// difference is exactly why the ruleset is configuration and not code.</item>
    /// </list>
    /// </summary>
    public static Ruleset OnePieceRuleset(int tcgId) => new(
        tcgId: tcgId,
        winPoints: 3,
        drawPoints: 1,
        lossPoints: 0,
        byePoints: 3,
        gameWinPoints: 3,
        gameDrawPoints: 1,
        gameLossPoints: 0,
        byesCountInOwnWinPercentage: false,
        minimumPercentage: PercentageFloor,
        swissPhase: new PhaseSettings(MatchFormat.Bo1, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5)),
        topCutPhase: new PhaseSettings(MatchFormat.Bo3, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(10)),
        tiebreakers:
        [
            Tiebreaker.OwnMatchWinPercentage,
            Tiebreaker.OpponentsMatchWinPercentage,
            Tiebreaker.HeadToHead
        ],
        timeExpiry: TimeExpiryPolicy.LeaderWins,
        rulesVersion: "Bandai OP Tournament Rules Manual, 17 oct 2025",
        officialSource: "https://en.onepiece-cardgame.com/pdf/tournament_rules_manual.pdf");
}
