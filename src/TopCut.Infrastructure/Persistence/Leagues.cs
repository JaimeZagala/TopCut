using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Common;
using TopCut.Domain.Leagues;
using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// The store's seasonal leagues (ADR-0036).
/// <para>
/// No <c>IgnoreQueryFilters</c> anywhere: the tenant filter is what makes every id here mean "one
/// of mine", so a league from another store reads exactly like one that does not exist.
/// </para>
/// </summary>
public sealed class Leagues(
    IDbContextFactory<TopCutDbContext> contexts,
    ITournamentRepository tournaments,
    ITenantContext tenant) : ILeagues
{
    public async Task<IReadOnlyList<LeagueRow>> ForCurrentStoreAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var rows = await db.Leagues
            .AsNoTracking()
            .Select(league => new
            {
                league.Id,
                league.Name,
                league.TcgId,
                league.StartsOn,
                league.EndsOn,
                league.IsOpen,
                Tournaments = db.Tournaments.Count(tournament => tournament.LeagueId == league.Id)
            })
            .ToListAsync(cancellationToken);

        var games = await GameNamesAsync(db, cancellationToken);

        return
        [
            // The season being run right now is the one the store came here for; the finished ones
            // are history and can wait at the bottom.
            .. rows
                .OrderByDescending(row => row.IsOpen)
                .ThenByDescending(row => row.StartsOn)
                .Select(row => new LeagueRow(
                    row.Id,
                    row.Name,
                    games.GetValueOrDefault(row.TcgId, string.Empty),
                    row.StartsOn,
                    row.EndsOn,
                    row.IsOpen,
                    row.Tournaments))
        ];
    }

    public async Task<LeagueDetails?> DetailsAsync(
        Guid leagueId,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var league = await db.Leagues
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == leagueId, cancellationToken);

        if (league is null)
        {
            return null;
        }

        var tournaments = await db.Tournaments
            .AsNoTracking()
            .Where(tournament => tournament.LeagueId == leagueId)
            .OrderBy(tournament => tournament.StartsAt)
            .Select(tournament => new LeagueTournamentRow(
                tournament.Id,
                tournament.Name,
                tournament.StartsAt,
                tournament.Status,
                tournament.Participants.Count(p => p.Status != ParticipantStatus.Dropped)))
            .ToListAsync(cancellationToken);

        var games = await GameNamesAsync(db, cancellationToken);

        return new LeagueDetails(
            league.Id,
            league.Name,
            league.TcgId,
            games.GetValueOrDefault(league.TcgId, string.Empty),
            league.StartsOn,
            league.EndsOn,
            league.IsOpen,
            tournaments);
    }

    public async Task<IReadOnlyList<LeagueOption>> OpenForAsync(
        int tcgId,
        Guid? include = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        return await db.Leagues
            .AsNoTracking()
            .Where(league => league.TcgId == tcgId && (league.IsOpen || league.Id == include))
            .OrderByDescending(league => league.StartsOn)
            .Select(league => new LeagueOption(league.Id, league.Name, league.IsOpen))
            .ToListAsync(cancellationToken);
    }

    public async Task<LeagueCreated> CreateAsync(
        LeagueDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return LeagueCreated.Fail("No hay ninguna tienda en contexto.");
        }

        await using var db = contexts.CreateDbContext();

        if (!await db.Tcgs.AnyAsync(tcg => tcg.Id == draft.TcgId && tcg.IsActive, cancellationToken))
        {
            return LeagueCreated.Fail("Ese juego no está disponible.");
        }

        League league;

        try
        {
            league = new League(tenantId, draft.TcgId, draft.Name, draft.StartsOn, draft.EndsOn);
        }
        catch (DomainException error)
        {
            return LeagueCreated.Fail(error.Message);
        }

        db.Leagues.Add(league);
        await db.SaveChangesAsync(cancellationToken);

        return LeagueCreated.Ok(league.Id);
    }

    public Task<OperationResult> UpdateAsync(
        Guid leagueId,
        LeagueDraft draft,
        CancellationToken cancellationToken = default) =>
        Change(leagueId, league => league.UpdateDetails(draft.Name, draft.StartsOn, draft.EndsOn), cancellationToken);

    public Task<OperationResult> CloseAsync(Guid leagueId, CancellationToken cancellationToken = default) =>
        Change(leagueId, league => league.Close(), cancellationToken);

    public Task<OperationResult> ReopenAsync(Guid leagueId, CancellationToken cancellationToken = default) =>
        Change(leagueId, league => league.Reopen(), cancellationToken);

    private async Task<OperationResult> Change(
        Guid leagueId,
        Action<League> change,
        CancellationToken cancellationToken)
    {
        await using var db = contexts.CreateDbContext();

        var league = await db.Leagues
            .SingleOrDefaultAsync(candidate => candidate.Id == leagueId, cancellationToken);

        if (league is null)
        {
            return OperationResult.Fail("No existe esa liga.");
        }

        try
        {
            change(league);
        }
        catch (DomainException error)
        {
            return OperationResult.Fail(error.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }

    /// <summary>
    /// The season's table. The aggregates are loaded whole and handed to the same calculator a
    /// single tournament uses, so the league cannot drift from what each event scored (ADR-0037).
    /// </summary>
    public Task<IReadOnlyList<LeagueStanding>> StandingsAsync(
        Guid leagueId,
        CancellationToken cancellationToken = default) =>
        tournaments.ReadLeagueAsync(leagueId, LeagueTable.For, cancellationToken);

    /// <summary>The catalog keyed by id: two rows in v1, cheaper read whole than joined per query.</summary>
    private static async Task<Dictionary<int, string>> GameNamesAsync(
        TopCutDbContext db,
        CancellationToken cancellationToken) =>
        await db.Tcgs.AsNoTracking().ToDictionaryAsync(tcg => tcg.Id, tcg => tcg.Name, cancellationToken);
}
