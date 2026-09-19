using TopCut.Domain.Catalogo;
using TopCut.Domain.Common;

namespace TopCut.Domain.Torneos;

/// <summary>
/// Una ronda del torneo, suiza o de top cut. El servidor guarda los <b>instantes de fin en UTC</b>,
/// no los segundos restantes: asi recargar la pagina o reconectar no reinicia la cuenta atras.
/// </summary>
public class Ronda : EntidadDeNegocio, IPerteneceATenant
{
    private readonly List<Emparejamiento> _emparejamientos = [];

    private Ronda() { }

    internal Ronda(Guid torneoId, Guid tenantId, int numero, Fase fase, ConfiguracionFase configuracion, DateTimeOffset ahora)
    {
        ExcepcionDeDominio.SiNo(numero >= 1, "La numeracion de rondas empieza en 1.");

        TorneoId = torneoId;
        TenantId = tenantId;
        Numero = numero;
        Fase = fase;
        Formato = configuracion.Formato;
        Estado = EstadoRonda.EnCurso;
        IniciadaEn = ahora;

        if (configuracion.DuracionRonda is { } duracion)
        {
            FinTiempoRegularEn = ahora + duracion;
            FinConProrrogaEn = ahora + duracion + configuracion.TiempoExtra;
        }
    }

    public Guid TorneoId { get; private set; }

    public Guid TenantId { get; private set; }

    public int Numero { get; private set; }

    public Fase Fase { get; private set; }

    /// <summary>Formato vigente en esta ronda, copiado de la fase al lanzarla.</summary>
    public FormatoPartida Formato { get; private set; }

    public EstadoRonda Estado { get; private set; }

    public DateTimeOffset IniciadaEn { get; private set; }

    /// <summary>Fin del tiempo de juego normal. Nulo si la fase no tiene limite.</summary>
    public DateTimeOffset? FinTiempoRegularEn { get; private set; }

    /// <summary>
    /// Fin de la prorroga (turnos extra). Coincide con <see cref="FinTiempoRegularEn"/> cuando el
    /// TCG no contempla tiempo extra de reloj, como en Magic.
    /// </summary>
    public DateTimeOffset? FinConProrrogaEn { get; private set; }

    public DateTimeOffset? CerradaEn { get; private set; }

    public IReadOnlyList<Emparejamiento> Emparejamientos => _emparejamientos.AsReadOnly();

    public bool TieneLimiteDeTiempo => FinConProrrogaEn is not null;

    public bool TodosLosResultadosReportados =>
        _emparejamientos.All(e => e.Estado == EstadoEmparejamiento.Reportado);

    /// <summary>Tiempo que queda de juego normal. Nulo si la ronda no tiene limite.</summary>
    public TimeSpan? TiempoRestante(DateTimeOffset ahora)
    {
        if (FinTiempoRegularEn is not { } fin)
        {
            return null;
        }

        var restante = fin - ahora;
        return restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
    }

    public bool ProrrogaAgotada(DateTimeOffset ahora) =>
        FinConProrrogaEn is { } fin && ahora >= fin;

    internal Emparejamiento Emparejar(int mesa, Guid participanteAId, Guid? participanteBId)
    {
        ExcepcionDeDominio.SiNo(Estado == EstadoRonda.EnCurso, "La ronda ya esta cerrada.");

        var emparejamiento = new Emparejamiento(Id, TenantId, mesa, participanteAId, participanteBId);
        _emparejamientos.Add(emparejamiento);
        return emparejamiento;
    }

    internal Emparejamiento EmparejamientoDe(Guid emparejamientoId) =>
        _emparejamientos.FirstOrDefault(e => e.Id == emparejamientoId)
        ?? throw new ExcepcionDeDominio("El emparejamiento no pertenece a esta ronda.");

    /// <summary>
    /// Agota el reloj: los enfrentamientos sin resultado quedan en empate. El organizador puede
    /// corregir cualquiera de ellos despues si los jugadores aclaran lo ocurrido.
    /// </summary>
    internal IReadOnlyList<Emparejamiento> ResolverPendientesPorTiempo()
    {
        var pendientes = _emparejamientos
            .Where(e => e.Estado == EstadoEmparejamiento.PendienteDeResultado)
            .ToList();

        foreach (var emparejamiento in pendientes)
        {
            emparejamiento.MarcarEmpatePorTiempoAgotado();
        }

        return pendientes;
    }

    internal void Cerrar(DateTimeOffset ahora)
    {
        ExcepcionDeDominio.SiNo(Estado == EstadoRonda.EnCurso, "La ronda ya estaba cerrada.");
        ExcepcionDeDominio.SiNo(
            TodosLosResultadosReportados,
            "No se puede cerrar una ronda con resultados pendientes.");

        Estado = EstadoRonda.Cerrada;
        CerradaEn = ahora;
    }
}
