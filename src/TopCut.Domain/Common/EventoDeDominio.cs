namespace TopCut.Domain.Common;

/// <summary>
/// Evento de negocio publicado al guardar cambios. En la v1 no hay ningun suscriptor: existen para
/// que anadir notificaciones (push o Web Push) sea escribir un handler y no tocar el dominio.
/// </summary>
public abstract record EventoDeDominio
{
    public DateTimeOffset OcurridoEn { get; init; } = DateTimeOffset.UtcNow;
}
