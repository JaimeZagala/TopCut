namespace TopCut.Domain.Tenencia;

/// <summary>
/// Cualquiera puede registrarse, pero una tienda no publica torneos hasta que un Superadmin la
/// aprueba. Es el control de coste de infraestructura descrito en docs/vision.md.
/// </summary>
public enum EstadoTienda
{
    PendienteDeAprobacion = 1,
    Aprobada = 2,
    Revocada = 3
}
