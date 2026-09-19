using TopCut.Domain.Common;

namespace TopCut.Domain.Torneos;

/// <summary>
/// Partida individual dentro de un enfrentamiento. En Bo1 hay una sola; en Bo3 hay dos o tres.
/// Se registra una por una porque el desempate por porcentaje de partidas ganadas lo necesita.
/// </summary>
public class Partida : EntidadDeNegocio, IPerteneceATenant
{
    private Partida() { }

    internal Partida(Guid emparejamientoId, Guid tenantId, int numero, GanadorPartida ganador)
    {
        ExcepcionDeDominio.SiNo(numero is >= 1 and <= 3, "El numero de partida debe estar entre 1 y 3.");

        EmparejamientoId = emparejamientoId;
        TenantId = tenantId;
        Numero = numero;
        Ganador = ganador;
    }

    public Guid EmparejamientoId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Orden dentro del enfrentamiento, empezando en 1.</summary>
    public int Numero { get; private set; }

    public GanadorPartida Ganador { get; private set; }
}
