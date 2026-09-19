using TopCut.Domain.Catalogo;
using TopCut.Domain.Common;

namespace TopCut.Domain.Torneos;

/// <summary>
/// Cruce de una ronda entre dos participantes, o un bye si <see cref="ParticipanteBId"/> es nulo.
/// Lleva control de concurrencia optimista porque los dos jugadores pueden reportar a la vez.
/// </summary>
public class Emparejamiento : EntidadDeNegocio, IPerteneceATenant, IConControlDeConcurrencia
{
    private readonly List<Partida> _partidas = [];

    private Emparejamiento() { }

    internal Emparejamiento(Guid rondaId, Guid tenantId, int mesa, Guid participanteAId, Guid? participanteBId)
    {
        ExcepcionDeDominio.SiNo(mesa >= 1, "El numero de mesa empieza en 1.");
        ExcepcionDeDominio.SiNo(
            participanteBId != participanteAId,
            "Un participante no puede enfrentarse a si mismo.");

        RondaId = rondaId;
        TenantId = tenantId;
        Mesa = mesa;
        ParticipanteAId = participanteAId;
        ParticipanteBId = participanteBId;

        // Un bye no se juega: nace ya resuelto.
        Estado = EsBye ? EstadoEmparejamiento.Reportado : EstadoEmparejamiento.PendienteDeResultado;
    }

    public Guid RondaId { get; private set; }

    public Guid TenantId { get; private set; }

    public int Mesa { get; private set; }

    public Guid ParticipanteAId { get; private set; }

    /// <summary>Nulo cuando el emparejamiento es un bye.</summary>
    public Guid? ParticipanteBId { get; private set; }

    public EstadoEmparejamiento Estado { get; private set; }

    public IReadOnlyList<Partida> Partidas => _partidas.AsReadOnly();

    /// <summary>Participante que reporto el resultado, si lo reporto un jugador y no la tienda.</summary>
    public Guid? ReportadoPor { get; private set; }

    /// <summary>La tienda corrigio el resultado, normalmente por discrepancia entre los jugadores.</summary>
    public bool CorregidoPorOrganizador { get; private set; }

    /// <summary>El resultado lo fijo el reloj, no los jugadores.</summary>
    public bool EmpatePorTiempoAgotado { get; private set; }

    public byte[]? RowVersion { get; set; }

    public bool EsBye => ParticipanteBId is null;

    public int VictoriasA => _partidas.Count(p => p.Ganador == GanadorPartida.ParticipanteA);

    public int VictoriasB => _partidas.Count(p => p.Ganador == GanadorPartida.ParticipanteB);

    public int PartidasEmpatadas => _partidas.Count(p => p.Ganador == GanadorPartida.Empate);

    /// <summary>Resultado visto desde el participante indicado.</summary>
    public ResultadoEnfrentamiento ResultadoDe(Guid participanteId)
    {
        ExcepcionDeDominio.SiNo(
            participanteId == ParticipanteAId || participanteId == ParticipanteBId,
            "Ese participante no juega este emparejamiento.");
        ExcepcionDeDominio.SiNo(
            Estado == EstadoEmparejamiento.Reportado,
            "El emparejamiento aun no tiene resultado.");

        if (EsBye)
        {
            return ResultadoEnfrentamiento.Bye;
        }

        if (VictoriasA == VictoriasB)
        {
            return ResultadoEnfrentamiento.Empate;
        }

        var ganaA = VictoriasA > VictoriasB;
        var esA = participanteId == ParticipanteAId;

        return ganaA == esA ? ResultadoEnfrentamiento.Victoria : ResultadoEnfrentamiento.Derrota;
    }

    /// <summary>Partidas ganadas por el participante indicado (para el desempate por partidas).</summary>
    public int PartidasGanadasDe(Guid participanteId)
    {
        ExcepcionDeDominio.SiNo(
            participanteId == ParticipanteAId || participanteId == ParticipanteBId,
            "Ese participante no juega este emparejamiento.");

        return participanteId == ParticipanteAId ? VictoriasA : VictoriasB;
    }

    /// <summary>Reporte de un jugador o alta directa por la tienda.</summary>
    internal void Reportar(IReadOnlyList<GanadorPartida> partidas, FormatoPartida formato, Guid? reportadoPor)
    {
        ExcepcionDeDominio.SiNo(!EsBye, "Un bye no admite resultado: ya esta resuelto.");
        Validar(partidas, formato);

        _partidas.Clear();
        for (var i = 0; i < partidas.Count; i++)
        {
            _partidas.Add(new Partida(Id, TenantId, i + 1, partidas[i]));
        }

        Estado = EstadoEmparejamiento.Reportado;
        ReportadoPor = reportadoPor;
        EmpatePorTiempoAgotado = false;
    }

    /// <summary>Correccion por la tienda, tipicamente ante una discrepancia entre los jugadores.</summary>
    internal void Corregir(IReadOnlyList<GanadorPartida> partidas, FormatoPartida formato)
    {
        Reportar(partidas, formato, reportadoPor: null);
        CorregidoPorOrganizador = true;
    }

    /// <summary>
    /// El cronometro llego a cero sin resultado reportado. La tienda puede corregirlo despues si
    /// los jugadores aclaran que habia pasado.
    /// </summary>
    internal void MarcarEmpatePorTiempoAgotado()
    {
        ExcepcionDeDominio.SiNo(!EsBye, "Un bye no puede quedar en empate por tiempo.");
        ExcepcionDeDominio.SiNo(
            Estado == EstadoEmparejamiento.PendienteDeResultado,
            "El emparejamiento ya tenia resultado.");

        _partidas.Clear();
        Estado = EstadoEmparejamiento.Reportado;
        EmpatePorTiempoAgotado = true;
        ReportadoPor = null;
    }

    private static void Validar(IReadOnlyList<GanadorPartida> partidas, FormatoPartida formato)
    {
        ExcepcionDeDominio.SiNo(partidas.Count > 0, "Hay que reportar al menos una partida.");
        ExcepcionDeDominio.SiNo(
            partidas.Count <= formato.PartidasMaximas(),
            $"Un enfrentamiento a {formato} no admite mas de {formato.PartidasMaximas()} partidas.");

        // Un enfrentamiento se detiene en cuanto alguien alcanza las victorias necesarias: si
        // llegan partidas posteriores, el reporte esta mal (2-1 con la tercera jugada de mas, por
        // ejemplo). Validarlo partida a partida evita que entren resultados imposibles.
        var necesarias = formato.VictoriasNecesarias();
        var a = 0;
        var b = 0;

        foreach (var ganador in partidas)
        {
            ExcepcionDeDominio.SiNo(
                a < necesarias && b < necesarias,
                $"El enfrentamiento ya estaba decidido con {a}-{b}: sobran partidas.");

            if (ganador == GanadorPartida.ParticipanteA)
            {
                a++;
            }
            else if (ganador == GanadorPartida.ParticipanteB)
            {
                b++;
            }
        }
    }
}
