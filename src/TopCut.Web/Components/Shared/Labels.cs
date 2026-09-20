using MudBlazor;
using TopCut.Domain.Catalog;
using TopCut.Domain.Tournaments;
using TopCut.Web.Resources;

namespace TopCut.Web.Components.Shared;

/// <summary>
/// Turns domain enums into what the organiser reads on screen. One place, so "Bo3" is worded the
/// same on the list, the form and the live view — and so a translation is one resource file away.
/// </summary>
public static class Labels
{
    public static string For(TournamentStatus status) => status switch
    {
        TournamentStatus.Draft => Textos.StatusDraft,
        TournamentStatus.Published => Textos.StatusPublished,
        TournamentStatus.InProgress => Textos.StatusInProgress,
        TournamentStatus.Completed => Textos.StatusCompleted,
        TournamentStatus.Cancelled => Textos.StatusCancelled,
        _ => status.ToString()
    };

    /// <summary>
    /// Colour of the status chip. Published is the one that means "players can see this", so it is
    /// the one that stands out.
    /// </summary>
    public static Color ColourFor(TournamentStatus status) => status switch
    {
        TournamentStatus.Draft => Color.Default,
        TournamentStatus.Published => Color.Primary,
        TournamentStatus.InProgress => Color.Success,
        TournamentStatus.Completed => Color.Dark,
        TournamentStatus.Cancelled => Color.Error,
        _ => Color.Default
    };

    public static string For(MatchFormat format) => format switch
    {
        MatchFormat.Bo1 => Textos.FormatBo1,
        MatchFormat.Bo3 => Textos.FormatBo3,
        _ => format.ToString()
    };

    public static string For(TimeExpiryPolicy policy) => policy switch
    {
        TimeExpiryPolicy.LeaderWins => Textos.TimeExpiryLeaderWins,
        TimeExpiryPolicy.AlwaysDraw => Textos.TimeExpiryAlwaysDraw,
        _ => policy.ToString()
    };

    public static string For(Phase phase) => phase switch
    {
        Phase.Swiss => Textos.PhaseSwiss,
        Phase.TopCut => Textos.PhaseTopCut,
        _ => phase.ToString()
    };

    /// <summary>
    /// The tiebreaker column headings. Short on purpose: the standings table has one column per
    /// tiebreaker and the organiser reads it sideways at a glance.
    /// </summary>
    public static string For(Tiebreaker tiebreaker) => tiebreaker switch
    {
        Tiebreaker.OwnMatchWinPercentage => Textos.TiebreakerOwnMatchWin,
        Tiebreaker.OpponentsMatchWinPercentage => Textos.TiebreakerOpponentsMatchWin,
        Tiebreaker.GameWinPercentage => Textos.TiebreakerGameWin,
        Tiebreaker.OpponentsGameWinPercentage => Textos.TiebreakerOpponentsGameWin,
        Tiebreaker.HeadToHead => Textos.TiebreakerHeadToHead,
        _ => tiebreaker.ToString()
    };

    /// <summary>A tiebreaker's value for one player, as a percentage.</summary>
    public static string Percentage(decimal value) => value.ToString("P1");

    /// <summary>"Top 8", or "sin top cut" when the tournament is Swiss only.</summary>
    public static string TopCut(int? size) =>
        size is { } players ? string.Format(Textos.TopCutOf, players) : Textos.TopCutNone;

    /// <summary>"5 rondas + top 8", or just the rounds when there is no cut.</summary>
    public static string Structure(int swissRounds, int? topCutSize) =>
        topCutSize is { } size
            ? string.Format(Textos.RoundsAndCutSummary, swissRounds, size)
            : string.Format(Textos.RoundsSummary, swissRounds);
}
