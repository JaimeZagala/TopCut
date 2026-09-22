using TopCut.Domain.Catalog;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Abstractions;

/// <summary>One row of the store's tournament list. A projection: nothing here is tracked.</summary>
public sealed record TournamentListItem(
    Guid Id,
    string Name,
    string TcgName,
    string TcgCode,
    DateTimeOffset StartsAt,
    TournamentStatus Status,
    int Capacity,
    int Registered,
    int Waitlisted,
    int SwissRounds,
    int? TopCutSize,
    string City)
{
    public bool IsFull => Registered >= Capacity;
}

/// <summary>Everything the edit screen shows for one tournament.</summary>
public sealed record TournamentDetails(
    Guid Id,
    string Name,
    int TcgId,
    string TcgName,
    DateTimeOffset StartsAt,
    Address Venue,
    int Capacity,
    int SwissRounds,
    int? TopCutSize,
    TournamentStatus Status,
    bool SettingsLocked,
    Guid? LeagueId,
    int Registered,
    int Waitlisted,
    PhaseSettings SwissPhase,
    PhaseSettings TopCutPhase,
    TimeExpiryPolicy TimeExpiry,
    string RulesVersion);

/// <summary>One player on a tournament's roster.</summary>
/// <param name="WaitlistPosition">Their place in the queue, or <c>null</c> when they hold a seat.</param>
/// <param name="HasAccount">
/// Whether they registered themselves from the application. Walk-ins the store typed in by hand
/// have no account, and renaming one of those is the store fixing its own typo rather than
/// overwriting what a player chose to be called.
/// </param>
public sealed record ParticipantRow(
    Guid Id,
    string Name,
    ParticipantStatus Status,
    int? WaitlistPosition,
    DateTimeOffset RegisteredAt,
    bool HasAccount);

/// <summary>A tournament's players, split the way the screen shows them.</summary>
public sealed record Roster(
    IReadOnlyList<ParticipantRow> Registered,
    IReadOnlyList<ParticipantRow> Waitlisted,
    IReadOnlyList<ParticipantRow> Dropped)
{
    public static readonly Roster Empty = new([], [], []);

    public int Count => Registered.Count + Waitlisted.Count + Dropped.Count;
}

/// <summary>
/// Read-only views of tournaments for the store's screens. Separate from
/// <see cref="ITournamentRepository"/> because these never load the aggregate: a list of thirty
/// tournaments has no business pulling in every round, pairing and game to show a date and a count.
/// </summary>
public interface ITournamentQueries
{
    /// <summary>The current store's tournaments, soonest first among those still to come.</summary>
    Task<IReadOnlyList<TournamentListItem>> ForCurrentStoreAsync(CancellationToken cancellationToken = default);

    /// <summary><c>null</c> when it does not exist or belongs to another store.</summary>
    Task<TournamentDetails?> DetailsAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Who is playing, who is waiting and who has dropped. Empty when the tournament does not exist
    /// or belongs to another store.
    /// </summary>
    Task<Roster> RosterAsync(Guid tournamentId, CancellationToken cancellationToken = default);
}
