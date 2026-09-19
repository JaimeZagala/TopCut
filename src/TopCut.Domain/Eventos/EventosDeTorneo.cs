using TopCut.Domain.Catalogo;
using TopCut.Domain.Common;

namespace TopCut.Domain.Eventos;

/// <summary>
/// Eventos de negocio del torneo. En la v1 <b>ningun suscriptor los consume</b>: existen para que
/// anadir notificaciones (Firebase en la app Android, Web Push en el navegador) o refrescar la
/// pantalla en vivo por SignalR sea escribir un handler, sin tocar la logica de dominio.
/// </summary>
public sealed record RondaLanzada(Guid TenantId, Guid TorneoId, Guid RondaId, int Numero, Fase Fase)
    : EventoDeDominio;

/// <summary>Se registro o se corrigio el resultado de un enfrentamiento.</summary>
public sealed record ResultadoRegistrado(Guid TenantId, Guid TorneoId, Guid RondaId, Guid EmparejamientoId)
    : EventoDeDominio;

/// <summary>
/// Se agoto el tiempo de la ronda y quedaban enfrentamientos sin reportar. Los afectados van en
/// <paramref name="EmparejamientosAfectados"/>, ya resueltos como empate.
/// </summary>
public sealed record ResultadoPendiente(
    Guid TenantId,
    Guid TorneoId,
    Guid RondaId,
    IReadOnlyList<Guid> EmparejamientosAfectados) : EventoDeDominio;

/// <summary>Quedo libre una plaza del torneo. <paramref name="PromocionadoId"/> es nulo si no habia lista de espera.</summary>
public sealed record PlazaLiberada(Guid TenantId, Guid TorneoId, Guid? PromocionadoId) : EventoDeDominio;

/// <summary>El torneo se dio por terminado.</summary>
public sealed record TorneoFinalizado(Guid TenantId, Guid TorneoId) : EventoDeDominio;
