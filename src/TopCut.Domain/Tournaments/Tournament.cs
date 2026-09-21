using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Events;
using TopCut.Domain.Tenancy;

namespace TopCut.Domain.Tournaments;

/// <summary>
/// Root of the tournament aggregate: participants, rounds and pairings are only ever changed
/// through it. The Swiss pairer and standings calculation arrive in milestone 2; what lives here is
/// the state and its invariants.
/// </summary>
public class Tournament : BusinessEntity, ITenantOwned, IAuditable, IHasRowVersion
{
    private readonly List<Participant> _participants = [];
    private readonly List<Round> _rounds = [];

    private Tournament() { }

    public static Tournament Create(
        Guid tenantId,
        Ruleset ruleset,
        string name,
        DateTimeOffset startsAt,
        Address venue,
        int capacity,
        int swissRounds)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "El torneo necesita un nombre.");
        DomainException.Require(capacity >= 2, "Un torneo necesita al menos 2 plazas.");
        DomainException.Require(swissRounds >= 1, "Un torneo necesita al menos una ronda suiza.");

        return new Tournament
        {
            TenantId = tenantId,
            TcgId = ruleset.TcgId,
            Name = name.Trim(),
            StartsAt = startsAt,
            Venue = venue,
            Capacity = capacity,
            SwissRounds = swissRounds,
            Status = TournamentStatus.Draft,
            Settings = TournamentSettings.CopyFrom(ruleset)
        };
    }

    public Guid TenantId { get; private set; }

    public int TcgId { get; private set; }

    public string Name { get; private set; } = null!;

    public DateTimeOffset StartsAt { get; private set; }

    /// <summary>Where it is played. Need not match the store's registered address.</summary>
    public Address Venue { get; private set; } = null!;

    public int Capacity { get; private set; }

    public int SwissRounds { get; private set; }

    /// <summary>Top cut size (4, 8, 16...). Null when the tournament is Swiss only.</summary>
    public int? TopCutSize { get; private set; }

    public TournamentStatus Status { get; private set; }

    /// <summary>Copy of the TCG ruleset taken at creation. Never resynchronised.</summary>
    public TournamentSettings Settings { get; private set; } = null!;

    /// <summary>The league this tournament scores towards, if any.</summary>
    public Guid? LeagueId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public byte[]? RowVersion { get; set; }

    public IReadOnlyList<Participant> Participants => _participants.AsReadOnly();

    public IReadOnlyList<Round> Rounds => _rounds.AsReadOnly();

    public bool HasTopCut => TopCutSize is not null;

    /// <summary>
    /// From the first round onwards the settings are frozen: changing the format or the top cut
    /// with games already played would invalidate the standings.
    /// </summary>
    public bool SettingsLocked =>
        Status is TournamentStatus.InProgress or TournamentStatus.Completed or TournamentStatus.Cancelled;

    public IEnumerable<Participant> Registered =>
        _participants.Where(p => p.Status == ParticipantStatus.Registered);

    public IEnumerable<Participant> Waitlist => _participants
        .Where(p => p.Status == ParticipantStatus.Waitlisted)
        .OrderBy(p => p.WaitlistPosition);

    /// <summary>Participants eligible for the next round's pairings.</summary>
    public IEnumerable<Participant> ActiveParticipants => _participants.Where(p => p.IsActive);

    public bool IsFull => Registered.Count() >= Capacity;

    public Round? CurrentRound => _rounds.FirstOrDefault(r => r.Status == RoundStatus.InProgress);

    // --- Lifecycle ------------------------------------------------------------------------------

    public void Publish()
    {
        DomainException.Require(Status == TournamentStatus.Draft, "Solo se publica un torneo en borrador.");
        Status = TournamentStatus.Published;
    }

    public void Unpublish()
    {
        DomainException.Require(Status == TournamentStatus.Published, "Solo se despublica un torneo publicado.");
        Status = TournamentStatus.Draft;
    }

    public void Cancel()
    {
        DomainException.Require(
            Status is TournamentStatus.Draft or TournamentStatus.Published or TournamentStatus.InProgress,
            "El torneo ya estaba cerrado.");

        Status = TournamentStatus.Cancelled;
    }

    public void Complete()
    {
        DomainException.Require(Status == TournamentStatus.InProgress, "Solo se finaliza un torneo en curso.");
        DomainException.Require(_rounds.All(r => r.Status == RoundStatus.Closed), "Hay rondas sin cerrar.");

        Status = TournamentStatus.Completed;
        Raise(new TournamentCompleted(TenantId, Id));
    }

    // --- Settings -------------------------------------------------------------------------------

    /// <summary>
    /// Turns the top cut on, resizes it or removes it. Allowed at any point until the first round
    /// starts, not just at creation time.
    /// </summary>
    public void SetTopCut(int? size)
    {
        DomainException.Require(!SettingsLocked, "El top cut no se puede tocar con el torneo ya en curso.");

        if (size is { } value)
        {
            DomainException.Require(value >= 2, "El top cut más pequeño es de 2 jugadores.");
            DomainException.Require(
                (value & (value - 1)) == 0,
                "El top cut debe ser una potencia de dos (2, 4, 8, 16...).");
        }

        TopCutSize = size;
    }

    public void SetPhaseSettings(Phase phase, PhaseSettings settings)
    {
        DomainException.Require(!SettingsLocked, "La configuración de fase queda congelada al empezar.");
        Settings.SetPhase(phase, settings);
    }

    /// <summary>Overrides what happens to unreported matches when the clock runs out (ADR-0009).</summary>
    public void SetTimeExpiryPolicy(TimeExpiryPolicy policy)
    {
        DomainException.Require(!SettingsLocked, "La configuración queda congelada al empezar.");
        Settings.SetTimeExpiryPolicy(policy);
    }

    public void UpdateDetails(string name, DateTimeOffset startsAt, Address venue, int capacity, int swissRounds)
    {
        DomainException.Require(!SettingsLocked, "El torneo ya está en curso.");
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "El torneo necesita un nombre.");
        DomainException.Require(capacity >= Registered.Count(), "No puedes dejar menos plazas que inscritos hay.");
        DomainException.Require(swissRounds >= 1, "Un torneo necesita al menos una ronda suiza.");

        Name = name.Trim();
        StartsAt = startsAt;
        Venue = venue;
        Capacity = capacity;
        SwissRounds = swissRounds;
    }

    public void AssignToLeague(Guid? leagueId)
    {
        DomainException.Require(!SettingsLocked, "El torneo ya está en curso.");
        LeagueId = leagueId;
    }

    // --- Participants ----------------------------------------------------------------------------

    /// <summary>
    /// Registers someone from the store's book of regulars. If the tournament is full they join the
    /// waitlist in arrival order. The user id is null for the players the store writes in by hand.
    /// <para>
    /// Signing up twice is refused, and it is the <b>book entry</b> that decides whether it is the
    /// same person — not the name, which two people can share, and not the account, which a walk-in
    /// does not have.
    /// </para>
    /// <para>
    /// Somebody who withdrew and comes back gets their <b>same</b> row reinstated. Two lines for one
    /// person would double-count them on the roster and make "am I in?" ambiguous. Coming back does
    /// not recover the seat, though: if it went to someone waiting, this is a place in the queue
    /// like anybody else's.
    /// </para>
    /// </summary>
    public Participant Register(Guid storePlayerId, string name, string? userId = null)
    {
        DomainException.Require(
            Status is TournamentStatus.Draft or TournamentStatus.Published,
            "El torneo ya no admite inscripciones.");

        var full = IsFull;
        var participant = EntryOfPlayer(storePlayerId);

        if (participant is not null)
        {
            DomainException.Require(
                participant.Status == ParticipantStatus.Dropped,
                "Ese jugador ya está inscrito en el torneo.");

            participant.Reinstate();
        }
        else
        {
            participant = new Participant(Id, TenantId, storePlayerId, name, userId);
            _participants.Add(participant);
        }

        if (full)
        {
            participant.MoveToWaitlist(Waitlist.Count() + 1);
        }

        Raise(new ParticipantRegistered(
            TenantId,
            Id,
            participant.Id,
            participant.Status == ParticipantStatus.Waitlisted));

        return participant;
    }

    /// <summary>
    /// The participant tied to that account, dropped ones included. Walk-ins the store typed in
    /// have no account, so they are never returned here even if the names match.
    /// </summary>
    public Participant? EntryOf(string userId) =>
        _participants.FirstOrDefault(participant => participant.UserId == userId);

    /// <summary>The participant for that entry in the store's book, dropped ones included.</summary>
    public Participant? EntryOfPlayer(Guid storePlayerId) =>
        _participants.FirstOrDefault(participant => participant.StorePlayerId == storePlayerId);

    /// <summary>
    /// Drops a participant. Before the tournament starts this frees a seat and promotes the first
    /// person waiting; once it is under way it only removes them from future pairings.
    /// Returns whoever was promoted, if anyone.
    /// </summary>
    public Participant? Drop(Guid participantId)
    {
        var participant = FindParticipant(participantId);

        // Once the first round is up, a seat is not really free: the pairings are made, and
        // dropping somebody in from the queue would leave them with rounds they never played.
        var freesSeat = participant.Status == ParticipantStatus.Registered
            && Status != TournamentStatus.InProgress;

        participant.Drop();

        var promoted = freesSeat ? Waitlist.FirstOrDefault() : null;
        promoted?.PromoteFromWaitlist();

        RenumberWaitlist();

        Raise(new ParticipantDropped(TenantId, Id, participant.Id, promoted?.Id));
        return promoted;
    }

    public void RenameParticipant(Guid participantId, string name) =>
        FindParticipant(participantId).Rename(name);

    // --- Rounds -----------------------------------------------------------------------------------

    /// <summary>
    /// Opens an empty round of the given phase. The Swiss pairer (milestone 2) fills it by calling
    /// Pair; this operation only sets the clock and the state.
    /// </summary>
    public Round StartRound(Phase phase, DateTimeOffset now)
    {
        DomainException.Require(
            Status is TournamentStatus.Published or TournamentStatus.InProgress,
            "El torneo no está en disposición de jugar rondas.");
        DomainException.Require(CurrentRound is null, "Hay una ronda en curso sin cerrar.");
        DomainException.Require(phase == Phase.Swiss || HasTopCut, "Este torneo no tiene top cut configurado.");
        DomainException.Require(
            phase != Phase.Swiss || _rounds.Count(r => r.Phase == Phase.Swiss) < SwissRounds,
            "Ya se jugaron todas las rondas suizas previstas.");

        var round = new Round(Id, TenantId, _rounds.Count + 1, phase, Settings.SettingsFor(phase), now);
        _rounds.Add(round);
        Status = TournamentStatus.InProgress;

        Raise(new RoundStarted(TenantId, Id, round.Id, round.Number, phase));
        return round;
    }

    /// <summary>Records one finished game as it happens, closing the match when it is decided.</summary>
    public void RecordGame(Guid roundId, Guid pairingId, GameWinner winner)
    {
        var round = FindRound(roundId);
        var pairing = round.PairingWithId(pairingId);

        pairing.RecordGame(winner, round.Format);

        if (pairing.Status == PairingStatus.Reported)
        {
            Raise(new ResultRecorded(TenantId, Id, roundId, pairingId));
        }
    }

    public void ReportResult(Guid roundId, Guid pairingId, IReadOnlyList<GameWinner> games, Guid? reportedBy)
    {
        var round = FindRound(roundId);
        round.PairingWithId(pairingId).Report(games, round.Format, reportedBy);
        Raise(new ResultRecorded(TenantId, Id, roundId, pairingId));
    }

    public void AmendResult(Guid roundId, Guid pairingId, IReadOnlyList<GameWinner> games)
    {
        var round = FindRound(roundId);
        round.PairingWithId(pairingId).Amend(games, round.Format);
        Raise(new ResultRecorded(TenantId, Id, roundId, pairingId));
    }

    /// <summary>
    /// Applies the round clock running out. Matches still unreported are settled under the
    /// tournament's time expiry policy, and the organiser is told which ones so they can review.
    /// </summary>
    public void ApplyTimeExpiry(Guid roundId, DateTimeOffset now)
    {
        var round = FindRound(roundId);

        DomainException.Require(round.ExtraTimeExpired(now), "La ronda todavía tiene tiempo.");

        var affected = round.ResolvePendingOnTimeExpiry(Settings.TimeExpiry);

        if (affected.Count > 0)
        {
            Raise(new RoundTimeExpired(TenantId, Id, roundId, [.. affected.Select(p => p.Id)]));
        }
    }

    public void CloseRound(Guid roundId, DateTimeOffset now)
    {
        var round = FindRound(roundId);

        round.Close(now);
        Raise(new RoundClosed(TenantId, Id, round.Id, round.Number));
    }

    // --- Helpers -----------------------------------------------------------------------------------

    /// <summary>
    /// Closes the gaps in the queue. Somebody leaving from the middle of it used to leave their
    /// number behind, so the person right after them was still told they were fourth when there
    /// were three people in front. Nobody noticed while only the store could see the list.
    /// </summary>
    private void RenumberWaitlist()
    {
        var position = 1;

        foreach (var waiting in Waitlist)
        {
            waiting.SetWaitlistPosition(position++);
        }
    }

    private Participant FindParticipant(Guid participantId) =>
        _participants.FirstOrDefault(p => p.Id == participantId)
        ?? throw new DomainException("Ese participante no está en el torneo.");

    private Round FindRound(Guid roundId) =>
        _rounds.FirstOrDefault(r => r.Id == roundId)
        ?? throw new DomainException("Esa ronda no pertenece al torneo.");
}
