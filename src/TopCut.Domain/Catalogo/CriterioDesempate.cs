namespace TopCut.Domain.Catalogo;

/// <summary>
/// Criterios de desempate disponibles. Cada TCG declara cuales usa y en que orden, porque no
/// coinciden: Magic usa tres niveles apoyados en partidas individuales y One Piece solo dos,
/// al jugarse su fase suiza a partida unica.
/// </summary>
public enum CriterioDesempate
{
    /// <summary>
    /// Porcentaje de victorias del propio jugador. Primer desempate en One Piece, donde los byes
    /// se excluyen del calculo y por tanto dos jugadores con los mismos puntos pueden diferir.
    /// </summary>
    PorcentajeVictoriasPropio = 1,

    /// <summary>Media del porcentaje de victorias de los rivales (el "OMW%" de Magic).</summary>
    PorcentajeVictoriasRivales = 2,

    /// <summary>Porcentaje de partidas individuales ganadas por el jugador (el "GW%" de Magic).</summary>
    PorcentajePartidasGanadas = 3,

    /// <summary>Media del porcentaje de partidas ganadas por los rivales (el "OGW%" de Magic).</summary>
    PorcentajePartidasGanadasRivales = 4,

    /// <summary>
    /// Resultado del enfrentamiento directo entre los empatados, si llegaron a jugarlo.
    /// One Piece lo prescribe como ultimo recurso antes de recurrir al azar.
    /// </summary>
    EnfrentamientoDirecto = 5
}
