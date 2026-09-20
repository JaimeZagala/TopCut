using TopCut.Application.Abstractions;
using TopCut.Domain.Common;
using TopCut.Domain.Pairings;
using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Tournaments;

/// <summary>
/// Running a tournament: launching rounds, taking results, closing rounds. Both presentation layers
/// call this — the Blazor screens in process, the Web API over HTTP — so the rules never end up
/// duplicated in a controller.
/// <para>
/// Broken invariants come back as a failed <see cref="OperationResult"/> rather than an exception,
/// because they are things the organiser can act on ("this round still has results pending"), not
/// crashes.
/// </para>
/// </summary>
public sealed class TournamentService(ITournamentRepository tournaments, TimeProvider clock)
{
    /// <summary>
    /// Opens the next round: Swiss while Swiss rounds remain, then the bracket if the tournament
    /// has a top cut. The engine works out the pairings.
    /// </summary>
    public Task<OperationResult> StartNextRoundAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        Mutate(tournamentId, tournament => TournamentEngine.StartNextRound(tournament, clock.GetUtcNow()), cancellationToken);

    /// <summary>Records one finished game, closing the match as soon as it is decided.</summary>
    public Task<OperationResult> RecordGameAsync(
        Guid tournamentId,
        Guid roundId,
        Guid pairingId,
        GameWinner winner,
        CancellationToken cancellationToken = default) =>
        Mutate(tournamentId, tournament => tournament.RecordGame(roundId, pairingId, winner), cancellationToken);

    /// <summary>A whole match reported at once, by a player or by the store.</summary>
    public Task<OperationResult> ReportResultAsync(
        Guid tournamentId,
        Guid roundId,
        Guid pairingId,
        IReadOnlyList<GameWinner> games,
        Guid? reportedBy,
        CancellationToken cancellationToken = default) =>
        Mutate(tournamentId, tournament => tournament.ReportResult(roundId, pairingId, games, reportedBy), cancellationToken);

    /// <summary>The store correcting a result, usually because the two players disagreed.</summary>
    public Task<OperationResult> AmendResultAsync(
        Guid tournamentId,
        Guid roundId,
        Guid pairingId,
        IReadOnlyList<GameWinner> games,
        CancellationToken cancellationToken = default) =>
        Mutate(tournamentId, tournament => tournament.AmendResult(roundId, pairingId, games), cancellationToken);

    /// <summary>Settles whatever is still unreported once the clock is spent, per the ruleset.</summary>
    public Task<OperationResult> ApplyTimeExpiryAsync(
        Guid tournamentId,
        Guid roundId,
        CancellationToken cancellationToken = default) =>
        Mutate(tournamentId, tournament => tournament.ApplyTimeExpiry(roundId, clock.GetUtcNow()), cancellationToken);

    public Task<OperationResult> CloseRoundAsync(
        Guid tournamentId,
        Guid roundId,
        CancellationToken cancellationToken = default) =>
        Mutate(tournamentId, tournament => tournament.CloseRound(roundId, clock.GetUtcNow()), cancellationToken);

    public Task<OperationResult> CompleteAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        Mutate(tournamentId, tournament => tournament.Complete(), cancellationToken);

    /// <summary>
    /// The current table, including results already in from the round in progress. Empty when the
    /// tournament does not exist or belongs to another store.
    /// </summary>
    public Task<IReadOnlyList<Standing>> GetStandingsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        tournaments.ReadAsync<IReadOnlyList<Standing>>(
            tournamentId,
            StandingsCalculator.For,
            whenMissing: [],
            cancellationToken);

    /// <summary>Whether there is another round to launch, so the live screen can offer the button.</summary>
    public Task<bool> HasNextRoundAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        tournaments.ReadAsync(
            tournamentId,
            TournamentEngine.HasNextRound,
            whenMissing: false,
            cancellationToken);

    private Task<OperationResult> Mutate(
        Guid tournamentId,
        Action<Tournament> change,
        CancellationToken cancellationToken) =>
        tournaments.ExecuteAsync(tournamentId, tournament =>
        {
            try
            {
                change(tournament);
                return OperationResult.Ok();
            }
            catch (DomainException error)
            {
                return OperationResult.Fail(error.Message);
            }
        }, cancellationToken);
}
