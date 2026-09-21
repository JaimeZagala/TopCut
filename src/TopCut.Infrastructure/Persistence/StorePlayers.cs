using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Common;
using TopCut.Domain.Tenancy;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// The store's book of regulars (ADR-0035).
/// <para>
/// Two ways in, and they are deliberately different. The <b>counter</b> matches what the store
/// types against the store's own book, which is what a paper book has always done. An <b>online
/// sign-up</b> matches only on the account, never on the name: guessing there would merge two
/// people who happen to share one, with nobody in the room to notice.
/// </para>
/// </summary>
public sealed class StorePlayers(IDbContextFactory<TopCutDbContext> contexts, ITenantContext tenant)
    : IStorePlayers
{
    public async Task<IReadOnlyList<StorePlayerMatch>> SearchAsync(
        string text,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        await using var db = contexts.CreateDbContext();

        var search = StorePlayer.Normalize(text);

        // Starts-with first and contains after it: typing "ana" should offer Ana before Mariana.
        var rows = await Book(db)
            .Where(player => player.NormalizedName.Contains(search))
            .Select(player => new
            {
                player.Id,
                player.Name,
                player.NormalizedName,
                HasAccount = player.UserId != null,
                Tournaments = db.Participants.Count(p => p.StorePlayerId == player.Id)
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .OrderByDescending(row => row.NormalizedName.StartsWith(search, StringComparison.Ordinal))
                .ThenByDescending(row => row.Tournaments)
                .ThenBy(row => row.Name)
                .Take(limit)
                .Select(row => new StorePlayerMatch(row.Id, row.Name, row.HasAccount, row.Tournaments))
        ];
    }

    public async Task<IReadOnlyList<StorePlayerRow>> AllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var rows = await Book(db)
            .Select(player => new
            {
                player.Id,
                player.Name,
                player.NormalizedName,
                HasAccount = player.UserId != null,
                Tournaments = db.Participants.Count(p => p.StorePlayerId == player.Id),
                LastPlayedAt = db.Participants
                    .Where(p => p.StorePlayerId == player.Id)
                    .Max(p => (DateTimeOffset?)p.RegisteredAt)
            })
            .ToListAsync(cancellationToken);

        // Same name, two entries: usually somebody who played as a walk-in before opening an
        // account. Pointed at, never merged on its own.
        var byName = rows
            .GroupBy(row => row.NormalizedName)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(row => (row.Id, Other: group.First(other => other.Id != row.Id).Id)))
            .ToDictionary(pair => pair.Id, pair => pair.Other);

        return
        [
            .. rows
                .OrderBy(row => row.Name)
                .Select(row => new StorePlayerRow(
                    row.Id,
                    row.Name,
                    row.HasAccount,
                    row.Tournaments,
                    row.LastPlayedAt,
                    byName.TryGetValue(row.Id, out var other) ? other : null))
        ];
    }

    public async Task<StorePlayerMatch> ForCounterAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var storeId = tenant.TenantId
            ?? throw new InvalidOperationException("El mostrador necesita una tienda en contexto.");

        return await FindOrCreate(storeId, name, userId: null, cancellationToken);
    }

    public Task<StorePlayerMatch> ForPlayerAsync(
        Guid storeId,
        string userId,
        string displayName,
        CancellationToken cancellationToken = default) =>
        FindOrCreate(storeId, displayName, userId, cancellationToken);

    public async Task<StorePlayerMatch> AddAsync(string name, CancellationToken cancellationToken = default)
    {
        var storeId = tenant.TenantId
            ?? throw new InvalidOperationException("El mostrador necesita una tienda en contexto.");

        await using var db = contexts.CreateDbContext();

        var created = new StorePlayer(storeId, name);

        db.StorePlayers.Add(created);
        await db.SaveChangesAsync(cancellationToken);

        return new StorePlayerMatch(created.Id, created.Name, HasAccount: false, Tournaments: 0);
    }

    public async Task<OperationResult> RenameAsync(
        Guid storePlayerId,
        string name,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var player = await db.StorePlayers
            .SingleOrDefaultAsync(candidate => candidate.Id == storePlayerId, cancellationToken);

        if (player is null)
        {
            return OperationResult.Fail("Ese jugador no está en tu fichero.");
        }

        try
        {
            player.Rename(name);
        }
        catch (DomainException error)
        {
            return OperationResult.Fail(error.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }

    private async Task<StorePlayerMatch> FindOrCreate(
        Guid storeId,
        string name,
        string? userId,
        CancellationToken cancellationToken)
    {
        await using var db = contexts.CreateDbContext();

        var existing = await Match(db, storeId, name, userId, cancellationToken);

        if (existing is not null)
        {
            return await AsMatch(db, existing, cancellationToken);
        }

        var created = new StorePlayer(storeId, name, userId);

        db.StorePlayers.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two sign-ups for the same account landing at once. One of them lost the unique index
            // on (TenantId, UserId), which is exactly what it is there for; read back the winner.
            await using var retry = contexts.CreateDbContext();

            var winner = await Match(retry, storeId, name, userId, cancellationToken);

            if (winner is null)
            {
                throw;
            }

            return await AsMatch(retry, winner, cancellationToken);
        }

        return new StorePlayerMatch(created.Id, created.Name, userId is not null, Tournaments: 0);
    }

    /// <summary>
    /// The account first, because it is the only link that cannot be wrong. Falling back to the
    /// name is for the counter, where the store is looking at its own book as it types.
    /// </summary>
    private static async Task<StorePlayer?> Match(
        TopCutDbContext db,
        Guid storeId,
        string name,
        string? userId,
        CancellationToken cancellationToken)
    {
        var book = db.StorePlayers
            .IgnoreQueryFilters()
            .Where(player => player.TenantId == storeId && player.MergedIntoId == null);

        if (userId is not null)
        {
            return await book.SingleOrDefaultAsync(player => player.UserId == userId, cancellationToken);
        }

        var normalized = StorePlayer.Normalize(name);

        // The one the store has used most, when an unmerged duplicate is still sitting there.
        return await book
            .Where(player => player.NormalizedName == normalized)
            .OrderByDescending(player => db.Participants.Count(p => p.StorePlayerId == player.Id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<StorePlayerMatch> AsMatch(
        TopCutDbContext db,
        StorePlayer player,
        CancellationToken cancellationToken) =>
        new(
            player.Id,
            player.Name,
            player.UserId is not null,
            await db.Participants
                .IgnoreQueryFilters()
                .CountAsync(p => p.StorePlayerId == player.Id, cancellationToken));

    /// <summary>The current store's book, merged entries left out: nobody signs up as a duplicate.</summary>
    private static IQueryable<StorePlayer> Book(TopCutDbContext db) =>
        db.StorePlayers.AsNoTracking().Where(player => player.MergedIntoId == null);
}
