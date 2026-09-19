using TopCut.Domain.Common;

namespace TopCut.Domain.Ligas;

/// <summary>
/// Liga por temporada: agrupa varios torneos de una tienda y acumula sus puntos usando el mismo
/// modelo de puntuacion que un torneo suelto. La clasificacion acumulada llega en el hito 6.
/// </summary>
public class Liga : EntidadDeNegocio, IPerteneceATenant, IAuditable
{
    private Liga() { }

    public Liga(Guid tenantId, int tcgId, string nombre, DateOnly comienza, DateOnly termina)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "La liga necesita un nombre.");
        ExcepcionDeDominio.SiNo(termina >= comienza, "La temporada no puede terminar antes de empezar.");

        TenantId = tenantId;
        TcgId = tcgId;
        Nombre = nombre.Trim();
        Comienza = comienza;
        Termina = termina;
        Abierta = true;
    }

    public Guid TenantId { get; private set; }

    public int TcgId { get; private set; }

    public string Nombre { get; private set; } = null!;

    public DateOnly Comienza { get; private set; }

    public DateOnly Termina { get; private set; }

    /// <summary>Una liga cerrada ya no admite que se le asignen torneos nuevos.</summary>
    public bool Abierta { get; private set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset? ModificadoEn { get; set; }

    public void Cerrar()
    {
        ExcepcionDeDominio.SiNo(Abierta, "La liga ya estaba cerrada.");
        Abierta = false;
    }

    public void Reabrir()
    {
        ExcepcionDeDominio.SiNo(!Abierta, "La liga ya estaba abierta.");
        Abierta = true;
    }
}
