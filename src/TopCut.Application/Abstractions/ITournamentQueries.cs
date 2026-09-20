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
    int Registered,
    int Waitlisted,
    PhaseSettings SwissPhase,
    PhaseSettings TopCutPhase,
    TimeExpiryPolicy TimeExpiry,
    string RulesVersion);

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
}
