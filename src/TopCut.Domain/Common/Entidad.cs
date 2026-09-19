namespace TopCut.Domain.Common;

/// <summary>
/// Raiz comun de las entidades del dominio. Acumula eventos de dominio que la capa de
/// infraestructura publica al guardar cambios (ver docs/arquitectura.md).
/// </summary>
public abstract class Entidad<TId> where TId : struct
{
    private readonly List<EventoDeDominio> _eventos = [];

    public TId Id { get; protected set; }

    public IReadOnlyCollection<EventoDeDominio> Eventos => _eventos.AsReadOnly();

    protected void Emitir(EventoDeDominio evento) => _eventos.Add(evento);

    public void LimpiarEventos() => _eventos.Clear();
}

/// <summary>
/// Entidad de negocio: clave <see cref="Guid"/> v7, secuencial en el tiempo, apta para URLs
/// publicas sin revelar el volumen de datos (ADR-0006).
/// </summary>
public abstract class EntidadDeNegocio : Entidad<Guid>
{
    protected EntidadDeNegocio() => Id = Guid.CreateVersion7();
}

/// <summary>
/// Entidad de catalogo: clave <see cref="int"/> autoincremental. Para tablas pequenas, estables y
/// no expuestas en URLs publicas (ADR-0006).
/// </summary>
public abstract class EntidadDeCatalogo : Entidad<int>;
