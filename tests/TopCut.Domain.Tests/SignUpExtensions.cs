using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests;

/// <summary>
/// Signs somebody up with a made-up entry in the store's book (ADR-0035). These tests are about the
/// tournament rules, not about who the store thinks the person is, so the book entry is a fresh id
/// each time — which is also the honest default: two different names are two different people.
/// </summary>
internal static class SignUpExtensions
{
    public static Participant SignUp(this Tournament tournament, string name, string? userId = null) =>
        tournament.Register(Guid.CreateVersion7(), name, userId);

    /// <summary>The same person signing up again, for the tests that care about that.</summary>
    public static Participant SignUpAgain(this Tournament tournament, Participant previous) =>
        tournament.Register(previous.StorePlayerId, previous.Name, previous.UserId);
}
