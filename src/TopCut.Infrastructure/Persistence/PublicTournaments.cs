using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// The public face of the catalog: the only queries in the application that deliberately reach
/// across stores (ADR-0029).
/// <para>
/// Everywhere else the tenant filter is the safety net, and it fails closed (ADR-0011). Here it is
/// switched off with <c>IgnoreQueryFilters</c>, so the rule about what may be shown has to be
/// written out by hand. It is written once, in <see cref="Visible"/>, and every method starts from
/// there — a filter that lives in one place cannot be half-applied.
/// </para>
/// </summary>
public sealed class PublicTournaments(IDbContextFactory<TopCutDbContext> contexts, TimeProvider clock)
    : IPublicTournaments
{
    public async Task<IReadOnlyList<PublicTournamentRow>> SearchAsync(
        TournamentSearch criteria,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var query = Visible(db);

        if (criteria.TcgId is { } tcgId)
        {
            query = query.Where(tournament => tournament.TcgId == tcgId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.City))
        {
            var city = criteria.City.Trim();
            query = query.Where(tournament => tournament.Venue.City == city);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Text))
        {
            // The player remembers either the event or the shop, and rarely spells either exactly.
            var text = $"%{criteria.Text.Trim()}%";

            query = query.Where(tournament =>
                EF.Functions.Like(tournament.Name, text)
                || db.Stores.Any(store => store.Id == tournament.TenantId && EF.Functions.Like(store.Name, text)));
        }

        if (criteria.From is { } from)
        {
            query = query.Where(tournament => tournament.StartsAt >= from);
        }

        if (criteria.Until is { } until)
        {
            query = query.Where(tournament => tournament.StartsAt <= until);
        }

        // Upcoming reads forwards from today and past reads backwards from it: in both cases what
        // the player cares about is the end nearest to now.
        var now = clock.GetUtcNow();

        query = criteria.Past
            ? query.Where(tournament => tournament.StartsAt < now).OrderByDescending(tournament => tournament.StartsAt)
            : query.Where(tournament => tournament.StartsAt >= now).OrderBy(tournament => tournament.StartsAt);

        var rows = await query
            .Select(tournament => new
            {
                tournament.Id,
                tournament.Name,
                tournament.TcgId,
                tournament.StartsAt,
                tournament.Status,
                tournament.Capacity,
                tournament.SwissRounds,
                tournament.TopCutSize,
                City = tournament.Venue.City,
                StoreName = db.Stores
                    .Where(store => store.Id == tournament.TenantId)
                    .Select(store => store.Name)
                    .First(),
                Registered = tournament.Participants.Count(p => p.Status == ParticipantStatus.Registered),
                Waitlisted = tournament.Participants.Count(p => p.Status == ParticipantStatus.Waitlisted)
            })
            .ToListAsync(cancellationToken);

        var games = await GamesAsync(db, cancellationToken);

        return
        [
            .. rows.Select(row => new PublicTournamentRow(
                row.Id,
                row.Name,
                row.StoreName,
                NameOf(games, row.TcgId),
                CodeOf(games, row.TcgId),
                row.StartsAt,
                row.City,
                row.Capacity,
                row.Registered,
                row.Waitlisted,
                row.SwissRounds,
                row.TopCutSize,
                row.Status))
        ];
    }

    public async Task<PublicTournamentInfo?> FindAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var tournament = await Visible(db)
            .SingleOrDefaultAsync(candidate => candidate.Id == tournamentId, cancellationToken);

        if (tournament is null)
        {
            return null;
        }

        var storeName = await db.Stores
            .AsNoTracking()
            .Where(store => store.Id == tournament.TenantId)
            .Select(store => store.Name)
            .SingleAsync(cancellationToken);

        // The participant rows carry the tenant filter as well, so this one needs the same opt-out:
        // without it the counts would come back as zero for every store but the visitor's own.
        var counts = await db.Participants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(participant => participant.TournamentId == tournamentId)
            .GroupBy(participant => participant.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var games = await GamesAsync(db, cancellationToken);

        return new PublicTournamentInfo(
            tournament.Id,
            tournament.Name,
            storeName,
            NameOf(games, tournament.TcgId),
            CodeOf(games, tournament.TcgId),
            tournament.StartsAt,
            tournament.Venue,
            tournament.Capacity,
            Count(counts.Select(entry => (entry.Status, entry.Count)), ParticipantStatus.Registered),
            Count(counts.Select(entry => (entry.Status, entry.Count)), ParticipantStatus.Waitlisted),
            tournament.SwissRounds,
            tournament.TopCutSize,
            tournament.Status,
            tournament.Settings.SwissPhase,
            tournament.Settings.TopCutPhase,
            tournament.Settings.TimeExpiry,
            tournament.Settings.RulesVersion);
    }

    public async Task<IReadOnlyList<string>> CitiesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        return await Visible(db)
            .Select(tournament => tournament.Venue.City)
            .Distinct()
            .OrderBy(city => city)
            .ToListAsync(cancellationToken);
    }

    /// <summary>What may be shown to anybody. The rule itself lives in <see cref="PublicVisibility"/>.</summary>
    private static IQueryable<Tournament> Visible(TopCutDbContext db) =>
        db.Tournaments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OnlyPublic(db);

    private static string NameOf(Dictionary<int, (string Code, string Name)> games, int tcgId) =>
        games.TryGetValue(tcgId, out var game) ? game.Name : string.Empty;

    private static string CodeOf(Dictionary<int, (string Code, string Name)> games, int tcgId) =>
        games.TryGetValue(tcgId, out var game) ? game.Code : string.Empty;

    private static int Count(IEnumerable<(ParticipantStatus Status, int Count)> counts, ParticipantStatus status) =>
        counts.FirstOrDefault(entry => entry.Status == status).Count;

    private static async Task<Dictionary<int, (string Code, string Name)>> GamesAsync(
        TopCutDbContext db,
        CancellationToken cancellationToken) =>
        await db.Tcgs
            .AsNoTracking()
            .ToDictionaryAsync(tcg => tcg.Id, tcg => (tcg.Code, tcg.Name), cancellationToken);
}
