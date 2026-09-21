using TopCut.Application.Abstractions;
using TopCut.Domain.Common;
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
public sealed class PlayerEntryService(ITournamentRepository tournaments)
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
        Participant? entry = null;

        var result = await tournaments.ExecuteOnPublicAsync(tournamentId, tournament =>
        {
            try
            {
                entry = tournament.Register(displayName, userId);
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
}
