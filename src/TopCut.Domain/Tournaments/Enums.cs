namespace TopCut.Domain.Tournaments;

public enum TournamentStatus
{
    /// <summary>Created by the store but not yet visible to players.</summary>
    Draft = 1,

    /// <summary>Listed in public search and taking registrations.</summary>
    Published = 2,

    /// <summary>First round started. Settings are frozen from here on.</summary>
    InProgress = 3,

    Completed = 4,
    Cancelled = 5
}

public enum ParticipantStatus
{
    Registered = 1,

    /// <summary>The tournament was full on registration. Only moves up when a seat frees.</summary>
    Waitlisted = 2,

    /// <summary>
    /// Dropped mid-tournament: out of future pairings and byes, but results already played still
    /// count towards the standings.
    /// </summary>
    Dropped = 3
}

public enum RoundStatus
{
    InProgress = 1,
    Closed = 2
}

public enum PairingStatus
{
    AwaitingResult = 1,
    Reported = 2
}

/// <summary>Winner of one individual game within a match.</summary>
public enum GameWinner
{
    PlayerA = 1,
    PlayerB = 2,
    Draw = 3
}
