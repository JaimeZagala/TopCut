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

    public async Task<OperationResult> MergeAsync(
        Guid keepId,
        Guid foldId,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        // The tenant filter is on StorePlayer, so a id from another store simply does not come
        // back: from here it reads the same as one that never existed.
        var both = await db.StorePlayers
            .Where(player => player.Id == keepId || player.Id == foldId)
            .ToListAsync(cancellationToken);

        var keep = both.SingleOrDefault(player => player.Id == keepId);
        var fold = both.SingleOrDefault(player => player.Id == foldId);

        if (keep is null || fold is null)
        {
            return OperationResult.Fail("Alguna de las dos fichas no está en tu fichero.");
        }

        var clash = await SharedTournament(db, keepId, foldId, cancellationToken);

        if (clash is not null)
        {
            // Both entries played the same tournament, so merging them would leave one person
            // twice on a roster and counted twice in the standings. The store unpicks it first.
            return OperationResult.Fail(
                $"Las dos fichas jugaron «{clash}». Da de baja una de las dos inscripciones antes de fusionarlas.");
        }

        string? account;

        try
        {
            account = fold.LeaveBookInto(keep);
        }
        catch (DomainException error)
        {
            return OperationResult.Fail(error.Message);
        }

        // Three writes that have to happen together and in this order.
        //
        // The order is not a preference: one account can only be on one live entry, and the unique
        // index will not tolerate both of them holding it even for the instant between two UPDATEs
        // of the same save — whose order EF does not promise. So the folded entry lets go, that
        // reaches the database, and only then does the survivor take it.
        //
        // And together, because a merge that stopped halfway would leave the person's tournaments
        // split between an entry nobody can see and one without their account. The retrying
        // strategy of the Azure free tier insists on owning the transaction, hence the wrapper.
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            if (account is not null && keep.UserId is null)
            {
                keep.LinkAccount(account);
                await db.SaveChangesAsync(cancellationToken);
            }

            // Everything the folded entry played is now the survivor's. Moved rather than followed
            // by a pointer at read time: one hop today is two hops after the next merge.
            await db.Participants
                .Where(participant => participant.StorePlayerId == foldId)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(participant => participant.StorePlayerId, keepId),
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        return OperationResult.Ok();
    }

    /// <summary>The name of a tournament both entries played, if there is one.</summary>
    private static async Task<string?> SharedTournament(
        TopCutDbContext db,
        Guid keepId,
        Guid foldId,
        CancellationToken cancellationToken)
    {
        var shared = await db.Participants
            .Where(participant => participant.StorePlayerId == keepId || participant.StorePlayerId == foldId)
            .GroupBy(participant => participant.TournamentId)
            .Where(group => group.Select(participant => participant.StorePlayerId).Distinct().Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefaultAsync(cancellationToken);

        if (shared == Guid.Empty)
        {
            return null;
        }

        return await db.Tournaments
            .Where(tournament => tournament.Id == shared)
            .Select(tournament => tournament.Name)
            .SingleOrDefaultAsync(cancellationToken);
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
