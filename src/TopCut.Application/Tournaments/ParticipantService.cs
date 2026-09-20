using TopCut.Application.Abstractions;
using TopCut.Domain.Common;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Tournaments;

/// <param name="Waitlisted">
/// Whether they went onto the waitlist instead of taking a seat. The organiser needs to know
/// straight away: telling someone at the counter that they are in when they are fifth in the queue
/// is the kind of mistake that is discovered when pairings go up.
/// </param>
public sealed record RegistrationOutcome(
    bool Succeeded,
    bool Waitlisted,
    int? WaitlistPosition,
    IReadOnlyList<string> Errors)
{
    public static RegistrationOutcome Ok(bool waitlisted, int? position) => new(true, waitlisted, position, []);

    public static RegistrationOutcome Fail(params string[] errors) => new(false, false, null, errors);
}

/// <param name="PromotedName">
/// Whoever moved up from the waitlist to take the freed seat, if anyone. Worth surfacing: that
/// player is expecting to be told they are in.
/// </param>
public sealed record DropOutcome(bool Succeeded, string? PromotedName, IReadOnlyList<string> Errors)
{
    public static DropOutcome Ok(string? promotedName) => new(true, promotedName, []);

    public static DropOutcome Fail(IEnumerable<string> errors) => new(false, null, [.. errors]);
}

/// <summary>
/// The roster: signing players in at the counter, fixing a misspelt name, and taking someone out.
/// <para>
/// Separate from <see cref="TournamentSetupService"/> (configuring the event) and from
/// <see cref="TournamentService"/> (running it) because it is a different moment with a different
/// screen — the queue at the door, not the settings and not the rounds.
/// </para>
/// </summary>
public sealed class ParticipantService(ITournamentRepository tournaments)
{
    /// <summary>
    /// Signs someone up by name. No account is involved: most people at a local event just turn up
    /// (docs/vision.md). A full tournament puts them on the waitlist rather than refusing them.
    /// </summary>
    public async Task<RegistrationOutcome> RegisterAsync(
        Guid tournamentId,
        string name,
        CancellationToken cancellationToken = default)
    {
        Participant? registered = null;

        var result = await tournaments.ExecuteAsync(tournamentId, tournament =>
        {
            try
            {
                registered = tournament.Register(name);
                return OperationResult.Ok();
            }
            catch (DomainException error)
            {
                return OperationResult.Fail(error.Message);
            }
        }, cancellationToken);

        return result.Succeeded && registered is not null
            ? RegistrationOutcome.Ok(
                registered.Status == ParticipantStatus.Waitlisted,
                registered.WaitlistPosition)
            : RegistrationOutcome.Fail([.. result.Errors]);
    }

    /// <summary>Fixes a name. Usually a typo made while typing fast at the counter.</summary>
    public Task<OperationResult> RenameAsync(
        Guid tournamentId,
        Guid participantId,
        string name,
        CancellationToken cancellationToken = default) =>
        Change(tournamentId, tournament => tournament.RenameParticipant(participantId, name), cancellationToken);

    /// <summary>
    /// Takes someone out. Before the tournament starts this frees a seat and the first person
    /// waiting moves up; once it is under way they only stop being paired, and everything they
    /// already played still counts towards the standings.
    /// </summary>
    public async Task<DropOutcome> DropAsync(
        Guid tournamentId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        string? promoted = null;

        var result = await tournaments.ExecuteAsync(tournamentId, tournament =>
        {
            try
            {
                promoted = tournament.Drop(participantId)?.Name;
                return OperationResult.Ok();
            }
            catch (DomainException error)
            {
                return OperationResult.Fail(error.Message);
            }
        }, cancellationToken);

        return result.Succeeded ? DropOutcome.Ok(promoted) : DropOutcome.Fail(result.Errors);
    }

    private Task<OperationResult> Change(
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
