namespace TopCut.Domain.Common;

/// <summary>
/// Se lanza cuando una operacion violaria una invariante del dominio. La capa de presentacion la
/// traduce a un 400/409 con <c>problem+json</c>; nunca debe llegar al usuario como error 500.
/// </summary>
public sealed class ExcepcionDeDominio(string mensaje) : Exception(mensaje)
{
    public static void SiNo(bool condicion, string mensaje)
    {
        if (!condicion)
        {
            throw new ExcepcionDeDominio(mensaje);
        }
    }
}
