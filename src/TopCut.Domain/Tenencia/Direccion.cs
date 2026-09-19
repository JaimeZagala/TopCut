using TopCut.Domain.Common;

namespace TopCut.Domain.Tenencia;

/// <summary>
/// Direccion estructurada, no una cadena libre. Se guarda descompuesta desde la v1 para que
/// activar "torneos cerca de mi" en la app movil sea anadir una consulta por distancia y no
/// rediseniar el modelo (ver docs/arquitectura.md).
/// </summary>
public sealed record Direccion
{
    private Direccion() { }

    public Direccion(string calle, string ciudad, string provincia, string codigoPostal)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(calle), "La direccion necesita una calle.");
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(ciudad), "La direccion necesita una ciudad.");
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(provincia), "La direccion necesita una provincia.");
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(codigoPostal), "La direccion necesita un codigo postal.");

        Calle = calle.Trim();
        Ciudad = ciudad.Trim();
        Provincia = provincia.Trim();
        CodigoPostal = codigoPostal.Trim();
    }

    public string Calle { get; private set; } = null!;

    public string Ciudad { get; private set; } = null!;

    public string Provincia { get; private set; } = null!;

    public string CodigoPostal { get; private set; } = null!;

    /// <summary>Reservado para la fase movil. En la v1 se guarda si se conoce, pero nada lo consulta.</summary>
    public double? Latitud { get; private set; }

    /// <summary>Reservado para la fase movil. En la v1 se guarda si se conoce, pero nada lo consulta.</summary>
    public double? Longitud { get; private set; }

    public Direccion ConCoordenadas(double latitud, double longitud)
    {
        ExcepcionDeDominio.SiNo(latitud is >= -90 and <= 90, "Latitud fuera de rango.");
        ExcepcionDeDominio.SiNo(longitud is >= -180 and <= 180, "Longitud fuera de rango.");

        return this with { Latitud = latitud, Longitud = longitud };
    }
}
