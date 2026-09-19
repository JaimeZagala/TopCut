using TopCut.Domain.Catalog;
using TopCut.Domain.Common;

namespace TopCut.Domain.Tournaments;

/// <summary>
/// One round of the tournament, Swiss or top cut. The server stores the <b>end instants in UTC</b>
/// rather than seconds remaining, so reloading the page or reconnecting never restarts the clock.
/// </summary>
public class Round : BusinessEntity, ITenantOwned
{
    private readonly List<Pairing> _pairings = [];

    private Round() { }

    internal Round(Guid tournamentId, Guid tenantId, int number, Phase phase, PhaseSettings settings, DateTimeOffset now)
    {
        DomainException.Require(number >= 1, "La numeracion de rondas empieza en 1.");

        TournamentId = tournamentId;
        TenantId = tenantId;
        Number = number;
        Phase = phase;
        Format = settings.Format;
        Status = RoundStatus.InProgress;
        StartedAt = now;

        if (settings.RoundLength is { } length)
        {
            RegularTimeEndsAt = now + length;
            ExtraTimeEndsAt = now + length + settings.ExtraTime;
        }
    }

    public Guid TournamentId { get; private set; }

    public Guid TenantId { get; private set; }

    public int Number { get; private set; }

    public Phase Phase { get; private set; }

    /// <summary>The format in force for this round, copied from the phase when it started.</summary>
    public MatchFormat Format { get; private set; }

    public RoundStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>End of normal play. Null when the phase is untimed.</summary>
    public DateTimeOffset? RegularTimeEndsAt { get; private set; }

    /// <summary>
    /// End of overtime (extra turns). Equal to <see cref="RegularTimeEndsAt"/> when the TCG does
    /// not treat overtime as clock time, as in Magic.
    /// </summary>
    public DateTimeOffset? ExtraTimeEndsAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<Pairing> Pairings => _pairings.AsReadOnly();

    public bool IsTimed => ExtraTimeEndsAt is not null;

    public bool AllResultsReported => _pairings.All(p => p.Status == PairingStatus.Reported);

    /// <summary>Time left in normal play. Null when the round is untimed.</summary>
    public TimeSpan? TimeRemaining(DateTimeOffset now)
    {
        if (RegularTimeEndsAt is not { } endsAt)
        {
            return null;
        }

        var remaining = endsAt - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public bool ExtraTimeExpired(DateTimeOffset now) => ExtraTimeEndsAt is { } endsAt && now >= endsAt;

    internal Pairing Pair(int table, Guid playerAId, Guid? playerBId)
    {
        DomainException.Require(Status == RoundStatus.InProgress, "La ronda ya esta cerrada.");

        var pairing = new Pairing(Id, TenantId, table, playerAId, playerBId);
        _pairings.Add(pairing);
        return pairing;
    }

    internal Pairing PairingWithId(Guid pairingId) =>
        _pairings.FirstOrDefault(p => p.Id == pairingId)
        ?? throw new DomainException("El emparejamiento no pertenece a esta ronda.");

    /// <summary>
    /// Applies the clock running out to every match still awaiting a result, following the
    /// tournament's time expiry policy. The organiser can amend any of them afterwards.
    /// </summary>
    internal IReadOnlyList<Pairing> ResolvePendingOnTimeExpiry(TimeExpiryPolicy policy)
    {
        var pending = _pairings
            .Where(p => p.Status == PairingStatus.AwaitingResult)
            .ToList();

        foreach (var pairing in pending)
        {
            pairing.ResolveOnTimeExpiry(policy);
        }

        return pending;
    }

    internal void Close(DateTimeOffset now)
    {
        DomainException.Require(Status == RoundStatus.InProgress, "La ronda ya estaba cerrada.");
        DomainException.Require(AllResultsReported, "No se puede cerrar una ronda con resultados pendientes.");

        Status = RoundStatus.Closed;
        ClosedAt = now;
    }
}
