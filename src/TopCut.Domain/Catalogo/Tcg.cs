using TopCut.Domain.Common;

namespace TopCut.Domain.Catalogo;

/// <summary>
/// Juego de cartas del catalogo global. Lo gestiona el Superadmin: activar un TCG nuevo es dar de
/// alta su fila y su <see cref="Ruleset"/>, no escribir codigo (ver docs/vision.md).
/// </summary>
public class Tcg : EntidadDeCatalogo
{
    private Tcg() { }

    public Tcg(string codigo, string nombre, bool activo = true)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(codigo), "El TCG necesita un codigo.");
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "El TCG necesita un nombre.");

        Codigo = codigo.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
        Activo = activo;
    }

    /// <summary>Codigo corto y estable, usado en URLs y filtros de busqueda (p. ej. <c>MTG</c>).</summary>
    public string Codigo { get; private set; } = null!;

    public string Nombre { get; private set; } = null!;

    /// <summary>Un TCG desactivado deja de ofrecerse al crear torneos, pero no afecta a los ya creados.</summary>
    public bool Activo { get; private set; }

    public Ruleset Ruleset { get; private set; } = null!;

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    public void Renombrar(string nombre)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "El TCG necesita un nombre.");
        Nombre = nombre.Trim();
    }
}
