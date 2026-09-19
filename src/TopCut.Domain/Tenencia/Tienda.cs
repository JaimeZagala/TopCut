using TopCut.Domain.Common;

namespace TopCut.Domain.Tenencia;

/// <summary>
/// Tienda organizadora. Es tambien el <b>tenant</b>: no hay una entidad separada, una tienda
/// <i>es</i> un tenant (ver docs/modelo-datos.md).
/// </summary>
public class Tienda : EntidadDeNegocio, IAuditable
{
    private Tienda() { }

    public Tienda(string nombre, Direccion direccion, bool esDePruebas = false)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "La tienda necesita un nombre.");

        Nombre = nombre.Trim();
        Direccion = direccion;
        EsDePruebas = esDePruebas;
        Estado = EstadoTienda.PendienteDeAprobacion;
    }

    public string Nombre { get; private set; } = null!;

    public Direccion Direccion { get; private set; } = null!;

    public EstadoTienda Estado { get; private set; }

    /// <summary>
    /// Tenant de pruebas: queda excluido de las busquedas publicas. Sustituye a un entorno de
    /// staging completo, que el nivel gratuito de Azure SQL no permite costear.
    /// </summary>
    public bool EsDePruebas { get; private set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset? ModificadoEn { get; set; }

    /// <summary>Puede crear y publicar torneos.</summary>
    public bool PuedeOrganizar => Estado == EstadoTienda.Aprobada;

    public void Aprobar()
    {
        ExcepcionDeDominio.SiNo(Estado != EstadoTienda.Aprobada, "La tienda ya estaba aprobada.");
        Estado = EstadoTienda.Aprobada;
    }

    public void RevocarAprobacion()
    {
        ExcepcionDeDominio.SiNo(Estado == EstadoTienda.Aprobada, "Solo se puede revocar una tienda aprobada.");
        Estado = EstadoTienda.Revocada;
    }

    public void CambiarDatos(string nombre, Direccion direccion)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "La tienda necesita un nombre.");
        Nombre = nombre.Trim();
        Direccion = direccion;
    }
}
