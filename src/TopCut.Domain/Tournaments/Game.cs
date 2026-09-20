using TopCut.Domain.Common;

namespace TopCut.Domain.Tournaments;

/// <summary>
/// One individual game within a match. Bo1 has a single game; Bo3 has two or three. They are
/// recorded one by one because the game-win-percentage tiebreaker needs them, and because the
/// leader-wins time expiry policy needs to know who was ahead when the clock ran out.
/// </summary>
public class Game : BusinessEntity, ITenantOwned
{
    private Game() { }

    internal Game(Guid pairingId, Guid tenantId, int number, GameWinner winner)
    {
        DomainException.Require(number is >= 1 and <= 3, "El número de partida debe estar entre 1 y 3.");

        PairingId = pairingId;
        TenantId = tenantId;
        Number = number;
        Winner = winner;
    }

    public Guid PairingId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Order within the match, starting at 1.</summary>
    public int Number { get; private set; }

    public GameWinner Winner { get; private set; }
}
