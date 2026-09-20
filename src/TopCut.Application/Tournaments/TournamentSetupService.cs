using TopCut.Application.Abstractions;
using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Tournaments;

/// <summary>What the store fills in before anyone plays. Every field is editable afterwards.</summary>
public sealed record TournamentDraft(
    int TcgId,
    string Name,
    DateTimeOffset StartsAt,
    string Street,
    string City,
    string Province,
    string PostalCode,
    int Capacity,
    int SwissRounds,
    int? TopCutSize);

/// <summary>
/// The rules the store can tune per tournament: match format and clock for each phase, and what
/// happens to a match still unreported when the clock runs out. Left out, the tournament keeps the
/// copy it took from the TCG's ruleset.
/// </summary>
public sealed record TournamentRules(
    PhaseSettings SwissPhase,
    PhaseSettings TopCutPhase,
    TimeExpiryPolicy TimeExpiry);

/// <param name="TournamentId">Set only when the tournament was created.</param>
public sealed record CreationResult(bool Succeeded, Guid? TournamentId, IReadOnlyList<string> Errors)
{
    public static CreationResult Ok(Guid tournamentId) => new(true, tournamentId, []);

    public static CreationResult Fail(params string[] errors) => new(false, null, errors);
}

/// <summary>
/// Setting a tournament up: creating it, changing its settings and publishing it. Kept apart from
/// <see cref="TournamentService"/>, which is about <i>running</i> one — different screens, different
/// moment, and putting both in one class would make a service nobody can read.
/// <para>
/// Broken rules come back as a failed result rather than an exception: "you cannot leave fewer
/// seats than people already signed up" is something the organiser can act on, not a crash.
/// </para>
/// </summary>
public sealed class TournamentSetupService(
    ITournamentRepository tournaments,
    ITcgCatalog catalog,
    ITenantContext tenant)
{
    public async Task<CreationResult> CreateAsync(
        TournamentDraft draft,
        TournamentRules? rules = null,
        CancellationToken cancellationToken = default)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return CreationResult.Fail("No hay ninguna tienda en contexto.");
        }

        // The tournament copies the ruleset as it stands today; correcting the ruleset later must
        // never rewrite the rules of a tournament that already exists (docs/vision.md).
        var ruleset = await catalog.ActiveRulesetForAsync(draft.TcgId, cancellationToken);

        if (ruleset is null)
        {
            return CreationResult.Fail("Ese juego no está disponible.");
        }

        Tournament tournament;

        try
        {
            tournament = Tournament.Create(
                tenantId,
                ruleset,
                draft.Name,
                draft.StartsAt,
                new Address(draft.Street, draft.City, draft.Province, draft.PostalCode),
                draft.Capacity,
                draft.SwissRounds);

            tournament.SetTopCut(draft.TopCutSize);
            Apply(tournament, rules);
        }
        catch (DomainException error)
        {
            return CreationResult.Fail(error.Message);
        }

        await tournaments.AddAsync(tournament, cancellationToken);
        return CreationResult.Ok(tournament.Id);
    }

    /// <summary>
    /// Saves the whole edit screen in <b>one</b> unit of work. Details and rules go together on
    /// purpose: split into four calls, a tournament could end up with the new capacity and the old
    /// round length because the third one was refused.
    /// </summary>
    public Task<OperationResult> UpdateAsync(
        Guid tournamentId,
        TournamentDraft draft,
        TournamentRules? rules = null,
        CancellationToken cancellationToken = default) =>
        Change(tournamentId, tournament =>
        {
            tournament.UpdateDetails(
                draft.Name,
                draft.StartsAt,
                new Address(draft.Street, draft.City, draft.Province, draft.PostalCode),
                draft.Capacity,
                draft.SwissRounds);

            tournament.SetTopCut(draft.TopCutSize);
            Apply(tournament, rules);
        }, cancellationToken);

    /// <summary>Makes it visible in public search and open to registrations.</summary>
    public Task<OperationResult> PublishAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        Change(tournamentId, tournament => tournament.Publish(), cancellationToken);

    public Task<OperationResult> UnpublishAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        Change(tournamentId, tournament => tournament.Unpublish(), cancellationToken);

    public Task<OperationResult> CancelAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        Change(tournamentId, tournament => tournament.Cancel(), cancellationToken);

    private static void Apply(Tournament tournament, TournamentRules? rules)
    {
        if (rules is null)
        {
            return;
        }

        tournament.SetPhaseSettings(Phase.Swiss, rules.SwissPhase);
        tournament.SetPhaseSettings(Phase.TopCut, rules.TopCutPhase);
        tournament.SetTimeExpiryPolicy(rules.TimeExpiry);
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
