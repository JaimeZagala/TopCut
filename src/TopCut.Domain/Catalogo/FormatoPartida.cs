namespace TopCut.Domain.Catalogo;

/// <summary>Numero de partidas de un enfrentamiento. Se configura por fase, no por torneo.</summary>
public enum FormatoPartida
{
    /// <summary>Partida unica. Formato de la fase suiza en One Piece Card Game.</summary>
    Bo1 = 1,

    /// <summary>Al mejor de tres. Formato estandar en Magic y en los top cut de One Piece.</summary>
    Bo3 = 3
}

public static class FormatoPartidaExtensiones
{
    /// <summary>Partidas que hay que ganar para llevarse el enfrentamiento.</summary>
    public static int VictoriasNecesarias(this FormatoPartida formato) => formato switch
    {
        FormatoPartida.Bo1 => 1,
        FormatoPartida.Bo3 => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(formato))
    };

    /// <summary>Partidas maximas que puede llegar a tener el enfrentamiento.</summary>
    public static int PartidasMaximas(this FormatoPartida formato) => (int)formato;
}
