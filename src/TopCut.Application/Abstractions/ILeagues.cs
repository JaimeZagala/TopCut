using TopCut.Domain.Tournaments;

namespace TopCut.Application.Abstractions;

/// <summary>What the store fills in for a league. The game is only read when creating one.</summary>
public sealed record LeagueDraft(int TcgId, string Name, DateOnly StartsOn, DateOnly EndsOn);

/// <summary>One row of the store's league list.</summary>
public sealed record LeagueRow(
    Guid Id,
    string Name,
    string TcgName,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsOpen,
    int Tournaments);

/// <summary>One of the tournaments scoring towards a league.</summary>
public sealed record LeagueTournamentRow(
    Guid Id,
    string Name,
    DateTimeOffset StartsAt,
    TournamentStatus Status,
    int Players);

/// <summary>A league and the tournaments pointing at it.</summary>
public sealed record LeagueDetails(
    Guid Id,
    string Name,
    int TcgId,
    string TcgName,
    DateOnly StartsOn,
    DateOnly EndsOn,
    bool IsOpen,
    IReadOnlyList<LeagueTournamentRow> Tournaments);

/// <summary>A league as the tournament's league picker lists it.</summary>
public sealed record LeagueOption(Guid Id, string Name, bool IsOpen);

/// <param name="LeagueId">Set only when the league was created.</param>
public sealed record LeagueCreated(bool Succeeded, Guid? LeagueId, IReadOnlyList<string> Errors)
{
    public static LeagueCreated Ok(Guid leagueId) => new(true, leagueId, []);

    public static LeagueCreated Fail(params string[] errors) => new(false, null, errors);
}

/// <summary>
/// The store's seasonal leagues. A league is a handful of rows with no aggregate hanging off it, so
/// reads and writes share one interface instead of the split <see cref="ITournamentQueries"/> and
/// <see cref="ITournamentRepository"/> a tournament needs.
/// </summary>
public interface ILeagues
{
    /// <summary>The current store's leagues, the ones still open first.</summary>
    Task<IReadOnlyList<LeagueRow>> ForCurrentStoreAsync(CancellationToken cancellationToken = default);

    /// <summary><c>null</c> when it does not exist or belongs to another store.</summary>
    Task<LeagueDetails?> DetailsAsync(Guid leagueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The leagues a tournament of that game may be assigned to: the store's open ones, plus
    /// <paramref name="include"/> so a tournament already in a closed league keeps showing it.
    /// <para>
    /// The same list answers the picker and the check behind it, which is on purpose: two ways of
    /// working out "can this tournament join that league?" would eventually disagree.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<LeagueOption>> OpenForAsync(
        int tcgId,
        Guid? include = null,
        CancellationToken cancellationToken = default);

    Task<LeagueCreated> CreateAsync(LeagueDraft draft, CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateAsync(
        Guid leagueId,
        LeagueDraft draft,
        CancellationToken cancellationToken = default);

    Task<OperationResult> CloseAsync(Guid leagueId, CancellationToken cancellationToken = default);

    Task<OperationResult> ReopenAsync(Guid leagueId, CancellationToken cancellationToken = default);
}
