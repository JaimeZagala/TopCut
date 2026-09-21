using TopCut.Domain.Common;

namespace TopCut.Domain.Tournaments;

/// <summary>
/// A player in one specific tournament. <b>Does not assume an account exists</b>: the store signs
/// up walk-ins by hand, and <see cref="UserId"/> stays empty for them.
/// </summary>
public class Participant : BusinessEntity, ITenantOwned, IHasRowVersion
{
    private Participant() { }

    internal Participant(Guid tournamentId, Guid tenantId, Guid storePlayerId, string name, string? userId)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "El participante necesita un nombre.");

        TournamentId = tournamentId;
        TenantId = tenantId;
        StorePlayerId = storePlayerId;
        Name = name.Trim();
        UserId = userId;
        Status = ParticipantStatus.Registered;
        RegisteredAt = DateTimeOffset.UtcNow;
    }

    public Guid TournamentId { get; private set; }

    /// <summary>
    /// Who this is in the store's book of regulars. Every participant has one, walk-ins included:
    /// it is what lets a league add up the same person across several tournaments, which a typed
    /// name cannot do.
    /// </summary>
    public Guid StorePlayerId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>
    /// The name shown on pairings and standings, as it was when they signed up. A snapshot on
    /// purpose: renaming somebody in the store's book fixes the book, not last month's pairings.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>Identity user, if the player registered from the application.</summary>
    public string? UserId { get; private set; }

    public ParticipantStatus Status { get; private set; }

    /// <summary>Place in the waitlist queue. <c>null</c> when not waitlisted.</summary>
    public int? WaitlistPosition { get; private set; }

    public DateTimeOffset RegisteredAt { get; private set; }

    public byte[]? RowVersion { get; set; }

    /// <summary>Eligible for the next round's pairings.</summary>
    public bool IsActive => Status == ParticipantStatus.Registered;

    internal void MoveToWaitlist(int position)
    {
        Status = ParticipantStatus.Waitlisted;
        WaitlistPosition = position;
    }

    internal void PromoteFromWaitlist()
    {
        DomainException.Require(
            Status == ParticipantStatus.Waitlisted,
            "Solo se promociona a quien está en lista de espera.");

        Status = ParticipantStatus.Registered;
        WaitlistPosition = null;
    }

    internal void SetWaitlistPosition(int position) => WaitlistPosition = position;

    /// <summary>Withdrawal. Keeps results already played and stops entering pairings and byes.</summary>
    internal void Drop()
    {
        DomainException.Require(
            Status != ParticipantStatus.Dropped,
            "El participante ya estaba dado de baja.");

        Status = ParticipantStatus.Dropped;
        WaitlistPosition = null;
    }

    /// <summary>
    /// Undoes a withdrawal. A player who signed up, changed their mind and changed it back gets
    /// their <b>same</b> row rather than a second one, so the roster keeps one line per person and
    /// the account stays tied to a single participant.
    /// </summary>
    internal void Reinstate()
    {
        DomainException.Require(
            Status == ParticipantStatus.Dropped,
            "Solo se reincorpora a quien se había dado de baja.");

        Status = ParticipantStatus.Registered;
        WaitlistPosition = null;
    }

    internal void Rename(string name)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "El participante necesita un nombre.");
        Name = name.Trim();
    }
}
