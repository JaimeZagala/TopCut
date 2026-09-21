using TopCut.Application.Abstractions;
using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Tournaments;

/// <param name="Waitlisted">They joined the queue instead of taking a seat.</param>
/// <param name="WaitlistPosition">Their place in it, so the screen can say a number and not "espera".</param>
public sealed record JoinOutcome(
    bool Succeeded,
    bool Waitlisted,
    int? WaitlistPosition,
    IReadOnlyList<string> Errors)
{
    public static JoinOutcome Ok(bool waitlisted, int? position) => new(true, waitlisted, position, []);

    public static JoinOutcome Fail(IEnumerable<string> errors) => new(false, false, null, [.. errors]);
}

/// <summary>
/// A player signing themselves up, and unsigning, from the public tournament sheet.
/// <para>
/// Separate from <see cref="ParticipantService"/>, which is the store's counter, because the two
/// differ in more than the screen: this one writes into <b>another store's</b> tournament
/// (ADR-0031), it identifies the player by their account rather than by a typed name, and it can
/// only touch that one player's own entry.
/// </para>
/// </summary>
public sealed class PlayerEntryService(
    ITournamentRepository tournaments,
    IStorePlayers book,
    TimeProvider clock)
{
    /// <summary>
    /// Signs the player up under the name on their account. A full tournament puts them in the
    /// queue rather than refusing them, same as at the counter.
    /// </summary>
    public async Task<JoinOutcome> JoinAsync(
        Guid tournamentId,
        string userId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        // Which store's book to write in. Read through the public path, so a tournament that is not
        // public answers the same here as everywhere else: as if it did not exist.
        var storeId = await tournaments.ReadOnPublicAsync(
            tournamentId,
            tournament => tournament.TenantId,
            whenMissing: Guid.Empty,
            cancellationToken);

        if (storeId == Guid.Empty)
        {
            return JoinOutcome.Fail(["No existe ese torneo."]);
        }

        // Matched on the account and never on the name (ADR-0035): nobody is in the room to catch
        // it if an online sign-up gets folded into the wrong regular.
        var player = await book.ForPlayerAsync(storeId, userId, displayName, cancellationToken);

        Participant? entry = null;

        var result = await tournaments.ExecuteOnPublicAsync(tournamentId, tournament =>
        {
            try
            {
                entry = tournament.Register(player.Id, player.Name, userId);
                return OperationResult.Ok();
            }
            catch (DomainException error)
            {
                return OperationResult.Fail(error.Message);
            }
        }, cancellationToken);

        return result.Succeeded && entry is not null
            ? JoinOutcome.Ok(entry.Status == ParticipantStatus.Waitlisted, entry.WaitlistPosition)
            : JoinOutcome.Fail(result.Errors);
    }

    /// <summary>
    /// Takes the player back out, freeing their seat for whoever is first in the queue.
    /// <para>
    /// Only before the first round. Once the tournament is under way a withdrawal changes the
    /// pairings, and the organiser standing in the room needs to know it happened — so from that
    /// point it is the desk's to do, not a button on a phone.
    /// </para>
    /// </summary>
    public Task<OperationResult> WithdrawAsync(
        Guid tournamentId,
        string userId,
        CancellationToken cancellationToken = default) =>
        tournaments.ExecuteOnPublicAsync(tournamentId, tournament =>
        {
            if (tournament.EntryOf(userId) is not { } entry || entry.Status == ParticipantStatus.Dropped)
            {
                return OperationResult.Fail("No estás inscrito en ese torneo.");
            }

            if (tournament.Status != TournamentStatus.Published)
            {
                return OperationResult.Fail(
                    "El torneo ya ha empezado. Habla con la tienda para que te dé de baja.");
            }

            try
            {
                tournament.Drop(entry.Id);
                return OperationResult.Ok();
            }
            catch (DomainException error)
            {
                return OperationResult.Fail(error.Message);
            }
        }, cancellationToken);

    /// <summary>
    /// The player's own view of a tournament they are in: their table this round, the clock, and
    /// where they stand. <c>null</c> when the tournament is not public or they are not in it — from
    /// outside, those are the same answer.
    /// </summary>
    public Task<PlayerBoard?> BoardAsync(
        Guid tournamentId,
        string userId,
        CancellationToken cancellationToken = default) =>
        tournaments.ReadOnPublicAsync<PlayerBoard?>(
            tournamentId,
            tournament => Build(tournament, userId, clock.GetUtcNow()),
            whenMissing: null,
            cancellationToken);

    /// <summary>
    /// The player reporting their own match. The games arrive already turned around into the
    /// tournament's own side (A first), which is the screen's job because only the screen knows
    /// which side the player was sitting on.
    /// <para>
    /// Refused once the match has a result. Two players correcting each other from their phones is
    /// how a table ends up with a score neither of them agreed to; from that point it is the desk's
    /// to fix, which is also how a signed result slip works.
    /// </para>
    /// </summary>
    public Task<OperationResult> ReportAsync(
        Guid tournamentId,
        string userId,
        Guid roundId,
        Guid pairingId,
        IReadOnlyList<GameWinner> games,
        CancellationToken cancellationToken = default) =>
        tournaments.ExecuteOnPublicAsync(tournamentId, tournament =>
        {
            if (tournament.EntryOf(userId) is not { } me)
            {
                return OperationResult.Fail("No estás inscrito en ese torneo.");
            }

            var round = tournament.Rounds.FirstOrDefault(candidate => candidate.Id == roundId);
            var pairing = round?.Pairings.FirstOrDefault(candidate => candidate.Id == pairingId);

            // Reporting somebody else's table is the one thing this screen must never allow, and
            // the pairing id travels in a form, so it is checked here and not only on the way out.
            if (pairing is null || (pairing.PlayerAId != me.Id && pairing.PlayerBId != me.Id))
            {
                return OperationResult.Fail("Esa mesa no es la tuya.");
            }

            if (pairing.Status == PairingStatus.Reported)
            {
                return OperationResult.Fail(
                    "Esa partida ya tiene resultado. Si no es el correcto, díselo a la tienda.");
            }

            try
            {
                tournament.ReportResult(roundId, pairingId, games, me.Id);
                return OperationResult.Ok();
            }
            catch (DomainException error)
            {
                return OperationResult.Fail(error.Message);
            }
        }, cancellationToken);

    private static PlayerBoard? Build(Tournament tournament, string userId, DateTimeOffset now)
    {
        if (tournament.EntryOf(userId) is not { } me)
        {
            return null;
        }

        var standing = StandingsCalculator.For(tournament).FirstOrDefault(row => row.ParticipantId == me.Id);
        var round = tournament.CurrentRound;

        var pairing = round?.Pairings.FirstOrDefault(candidate =>
            candidate.PlayerAId == me.Id || candidate.PlayerBId == me.Id);

        return new PlayerBoard(
            tournament.Id,
            tournament.Name,
            tournament.Status,
            me.Id,
            me.Name,
            me.Status,
            round?.Number,
            round?.Phase,
            round?.Status,
            round?.RegularTimeEndsAt,
            round?.ExtraTimeEndsAt,
            round is null || pairing is null ? null : MatchOf(pairing, round, me, tournament),
            standing?.Rank,
            standing?.MatchPoints ?? 0,
            standing?.Record ?? "0-0-0",
            now);
    }

    private static PlayerMatch MatchOf(Pairing pairing, Round round, Participant me, Tournament tournament)
    {
        var iAmPlayerA = pairing.PlayerAId == me.Id;
        var opponentId = iAmPlayerA ? pairing.PlayerBId : pairing.PlayerAId;

        return new PlayerMatch(
            round.Id,
            pairing.Id,
            pairing.Table,
            OpponentName: opponentId is { } other
                ? tournament.Participants.FirstOrDefault(player => player.Id == other)?.Name ?? string.Empty
                : string.Empty,
            pairing.IsBye,
            Reported: pairing.Status == PairingStatus.Reported,
            MyGames: iAmPlayerA ? pairing.PlayerAWins : pairing.PlayerBWins,
            OpponentGames: iAmPlayerA ? pairing.PlayerBWins : pairing.PlayerAWins,
            ReportedByMe: pairing.ReportedBy == me.Id,
            pairing.DecidedByTimeExpiry,
            pairing.AmendedByOrganizer,
            round.Format,
            iAmPlayerA);
    }
}
