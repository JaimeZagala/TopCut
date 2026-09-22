using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Abstractions;

/// <summary>One of a league's tournaments, as the public page lists them.</summary>
public sealed record PublicLeagueTournament(
    Guid Id,
    string Name,
    DateTimeOffset StartsAt,
    TournamentStatus Status,
    int Players);

/// <summary>A league's public page: the season's table and the events that fed it.</summary>
/// <param name="Viewer">
/// The book entry of whoever is reading, in this league's store, when they are signed in and have
/// played here. It is what lets the table say which row is theirs, which is the first thing anybody
/// looks for in a standings table.
/// </param>
public sealed record PublicLeagueInfo(
    Guid Id,
    string Name,
    string StoreName,
    string TcgName,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsOpen,
    IReadOnlyList<LeagueStanding> Standings,
    IReadOnlyList<PublicLeagueTournament> Tournaments,
    Guid? Viewer);

/// <summary>One league a player has scored in, with where they stand in it.</summary>
public sealed record PlayerLeagueRow(
    Guid LeagueId,
    string Name,
    string StoreName,
    string TcgName,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsOpen,
    int Rank,
    int Players,
    int Points,
    int Tournaments);

/// <summary>
/// Leagues as anyone sees them, signed in or not. Crosses the tenant boundary on purpose, like the
/// rest of the public face (ADR-0029).
/// <para>
/// A league is public <b>when at least one of its tournaments is</b>: that is the same rule as
/// everywhere else, asked of the league, and it means a season the store is still assembling in
/// drafts has no address to stumble on (ADR-0037).
/// </para>
/// </summary>
public interface IPublicLeagues
{
    /// <summary><c>null</c> when it does not exist or is not public, which read the same from outside.</summary>
    /// <param name="viewerUserId">
    /// Who is reading, when they are signed in. Taken from the authenticated principal and never
    /// from the address, which anybody can type.
    /// </param>
    Task<PublicLeagueInfo?> FindAsync(
        Guid leagueId,
        string? viewerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Whether that league has an address to share, for the store's own screen.</summary>
    Task<bool> IsPublicAsync(Guid leagueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The leagues this account has scored in, whichever store runs them, with their position in
    /// each. A league they signed up for but have not played yet has nothing to say and is left out.
    /// </summary>
    Task<IReadOnlyList<PlayerLeagueRow>> ForPlayerAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
