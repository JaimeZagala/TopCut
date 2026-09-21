namespace TopCut.Application.Abstractions;

/// <summary>One entry in a store's book of regulars, as the counter's autocomplete shows it.</summary>
/// <param name="Tournaments">How many of the store's tournaments they have played, so that two
/// people with similar names can be told apart by the one the store actually knows.</param>
public sealed record StorePlayerMatch(Guid Id, string Name, bool HasAccount, int Tournaments);

/// <summary>A row of the directory screen.</summary>
/// <param name="PossibleDuplicateOf">
/// Another entry with the same normalised name. Nothing is merged behind the store's back
/// (ADR-0035); this only points at the pair so somebody who knows both can decide.
/// </param>
public sealed record StorePlayerRow(
    Guid Id,
    string Name,
    bool HasAccount,
    int Tournaments,
    DateTimeOffset? LastPlayedAt,
    Guid? PossibleDuplicateOf);

/// <summary>
/// The store's book of regulars: who has played here, so a league can add up the same person across
/// several tournaments (ADR-0035).
/// </summary>
public interface IStorePlayers
{
    /// <summary>Suggestions for the counter, best match first. Current store only.</summary>
    Task<IReadOnlyList<StorePlayerMatch>> SearchAsync(
        string text,
        int limit = 8,
        CancellationToken cancellationToken = default);

    /// <summary>The whole book, for the directory screen. Current store only.</summary>
    Task<IReadOnlyList<StorePlayerRow>> AllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The entry to use for a name typed at the counter, creating it the first time that name is
    /// seen. Matching a typed name against the store's own book is what a paper book does; what is
    /// never guessed is the other direction, an online sign-up against a walk-in (ADR-0035).
    /// </summary>
    Task<StorePlayerMatch> ForCounterAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// The entry for an account at a given store, creating it if they have never played there.
    /// Crosses the tenant boundary on purpose, like the rest of the player's own path (ADR-0031):
    /// the store is the one that owns the tournament they are signing up for.
    /// </summary>
    Task<StorePlayerMatch> ForPlayerAsync(
        Guid storeId,
        string userId,
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A brand new entry even though the store already has one with that name. The escape hatch for
    /// the shop that really does have two Anas, and the reason the name has no unique index.
    /// </summary>
    Task<StorePlayerMatch> AddAsync(string name, CancellationToken cancellationToken = default);

    Task<OperationResult> RenameAsync(
        Guid storePlayerId,
        string name,
        CancellationToken cancellationToken = default);
}
