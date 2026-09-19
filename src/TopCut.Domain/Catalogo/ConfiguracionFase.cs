namespace TopCut.Domain.Catalogo;

/// <summary>
/// Ajustes de una fase del torneo (suiza o top cut). Son independientes entre fases: una tienda
/// puede combinar Bo3 en suizo con Bo1 en top cut si le conviene por tiempo de sala.
/// </summary>
/// <param name="Formato">Bo1 o Bo3.</param>
/// <param name="DuracionRonda">Tiempo de ronda. <c>null</c> = sin limite (finales de campeonato).</param>
/// <param name="TiempoExtra">
/// Prorroga tras agotarse el tiempo, para turnos extra. Cero en Magic, que no la contempla como
/// tiempo de reloj; 5 o 10 minutos en One Piece segun la fase.
/// </param>
public readonly record struct ConfiguracionFase(
    FormatoPartida Formato,
    TimeSpan? DuracionRonda,
    TimeSpan TiempoExtra)
{
    /// <summary>Tiempo total de reloj de la ronda, prorroga incluida.</summary>
    public TimeSpan? DuracionTotal => DuracionRonda is null ? null : DuracionRonda + TiempoExtra;
}
