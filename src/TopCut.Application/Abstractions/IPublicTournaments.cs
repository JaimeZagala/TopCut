using TopCut.Domain.Catalog;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Abstractions;

/// <summary>
/// What the player filled in on the public search form. Every field is optional: arriving at
/// <c>/torneos</c> with nothing typed is the normal case, and it means "what is coming up".
/// <para>
/// Written as init properties rather than positional parameters because the form will grow — a
/// distance filter lands with the mobile phase — and a new field should not have to be threaded
/// through every call site.
/// </para>
/// </summary>
public sealed record TournamentSearch
{
    /// <summary>Matches the tournament name or the store's, whichever the player remembers.</summary>
    public string? Text { get; init; }

    public string? City { get; init; }

    public int? TcgId { get; init; }

    /// <summary>Earliest start, as an instant: the screen turns the typed date into one.</summary>
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? Until { get; init; }

    /// <summary>
    /// Looks backwards instead of forwards. Two different questions — "where do I play on
    /// Saturday?" and "how did that one end?" — so they are two lists, each in its own order,
    /// rather than one long list the player has to scroll past the present to read.
    /// </summary>
    public bool Past { get; init; }
}

/// <summary>One tournament as the public list shows it.</summary>
public sealed record PublicTournamentRow(
    Guid Id,
    string Name,
    string StoreName,
    string TcgName,
    string TcgCode,
    DateTimeOffset StartsAt,
    string City,
    int Capacity,
    int Registered,
    int Waitlisted,
    int SwissRounds,
    int? TopCutSize,
    TournamentStatus Status)
{
    public bool IsFull => Registered >= Capacity;

    public int SeatsLeft => Math.Max(0, Capacity - Registered);
}

/// <summary>
/// The full sheet for one tournament: what a player needs to decide whether to turn up, including
/// the rules they will be playing under.
/// </summary>
public sealed record PublicTournamentInfo(
    Guid Id,
    string Name,
    string StoreName,
    string TcgName,
    string TcgCode,
    DateTimeOffset StartsAt,
    Address Venue,
    int Capacity,
    int Registered,
    int Waitlisted,
    int SwissRounds,
    int? TopCutSize,
    TournamentStatus Status,
    PhaseSettings SwissPhase,
    PhaseSettings TopCutPhase,
    TimeExpiryPolicy TimeExpiry,
    string RulesVersion,
    Guid? LeagueId,
    string? LeagueName)
{
    public bool IsFull => Registered >= Capacity;

    public int SeatsLeft => Math.Max(0, Capacity - Registered);

    public bool HasTopCut => TopCutSize is not null;

    /// <summary>
    /// Published and not yet under way. A tournament that has started, finished or been called off
    /// is still worth reading — somebody has it bookmarked — but there is nothing left to join.
    /// </summary>
    public bool AcceptsRegistrations => Status == TournamentStatus.Published;
}

/// <summary>
/// Tournaments as anyone sees them, signed in or not, whichever store is organising.
/// <para>
/// This is the <b>one place</b> in the application that reads across stores on purpose (ADR-0029).
/// Everything else goes through the tenant filter, which fails closed (ADR-0011); here the filter
/// is switched off and replaced by an explicit rule — an approved store that is not a test tenant,
/// and a tournament its organiser has published.
/// </para>
/// </summary>
public interface IPublicTournaments
{
    Task<IReadOnlyList<PublicTournamentRow>> SearchAsync(
        TournamentSearch criteria,
        CancellationToken cancellationToken = default);

    /// <summary><c>null</c> when it does not exist or is not public, which read the same from outside.</summary>
    Task<PublicTournamentInfo?> FindAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    /// <summary>The cities that actually have something to show, for the search form's list.</summary>
    Task<IReadOnlyList<string>> CitiesAsync(CancellationToken cancellationToken = default);
}
