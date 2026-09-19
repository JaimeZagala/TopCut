using TopCut.Domain.Catalogo;
using TopCut.Domain.Common;
using TopCut.Domain.Eventos;
using TopCut.Domain.Tenencia;

namespace TopCut.Domain.Torneos;

/// <summary>
/// Raiz del agregado de torneo: participantes, rondas y emparejamientos se modifican a traves de
/// el, nunca por separado. El emparejador suizo y el calculo de clasificacion llegan en el hito 2;
/// aqui vive solo el estado y sus invariantes.
/// </summary>
public class Torneo : EntidadDeNegocio, IPerteneceATenant, IAuditable, IConControlDeConcurrencia
{
    private readonly List<Participante> _participantes = [];
    private readonly List<Ronda> _rondas = [];

    private Torneo() { }

    public static Torneo Crear(
        Guid tenantId,
        Ruleset ruleset,
        string nombre,
        DateTimeOffset comienzaEn,
        Direccion lugar,
        int plazas,
        int rondasSuizas)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "El torneo necesita un nombre.");
        ExcepcionDeDominio.SiNo(plazas >= 2, "Un torneo necesita al menos 2 plazas.");
        ExcepcionDeDominio.SiNo(rondasSuizas >= 1, "Un torneo necesita al menos una ronda suiza.");

        return new Torneo
        {
            TenantId = tenantId,
            TcgId = ruleset.TcgId,
            Nombre = nombre.Trim(),
            ComienzaEn = comienzaEn,
            Lugar = lugar,
            Plazas = plazas,
            RondasSuizas = rondasSuizas,
            Estado = EstadoTorneo.Borrador,
            Configuracion = ConfiguracionTorneo.CopiarDe(ruleset)
        };
    }

    public Guid TenantId { get; private set; }

    public int TcgId { get; private set; }

    public string Nombre { get; private set; } = null!;

    public DateTimeOffset ComienzaEn { get; private set; }

    /// <summary>Donde se juega. Puede no coincidir con la direccion fiscal de la tienda.</summary>
    public Direccion Lugar { get; private set; } = null!;

    public int Plazas { get; private set; }

    public int RondasSuizas { get; private set; }

    /// <summary>Tamano del top cut (4, 8, 16...). Nulo si el torneo se juega solo a suizo.</summary>
    public int? TamanoTopCut { get; private set; }

    public EstadoTorneo Estado { get; private set; }

    /// <summary>Copia del ruleset del TCG tomada al crear el torneo. No se resincroniza nunca.</summary>
    public ConfiguracionTorneo Configuracion { get; private set; } = null!;

    /// <summary>Liga a la que puntua este torneo, si pertenece a alguna.</summary>
    public Guid? LigaId { get; private set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset? ModificadoEn { get; set; }

    public byte[]? RowVersion { get; set; }

    public IReadOnlyList<Participante> Participantes => _participantes.AsReadOnly();

    public IReadOnlyList<Ronda> Rondas => _rondas.AsReadOnly();

    public bool TieneTopCut => TamanoTopCut is not null;

    /// <summary>
    /// A partir de la primera ronda la configuracion queda congelada: cambiar el formato o el top
    /// cut con partidas ya jugadas invalidaria la clasificacion.
    /// </summary>
    public bool ConfiguracionCongelada =>
        Estado is EstadoTorneo.EnCurso or EstadoTorneo.Finalizado or EstadoTorneo.Cancelado;

    public IEnumerable<Participante> Inscritos =>
        _participantes.Where(p => p.Estado == EstadoParticipante.Inscrito);

    public IEnumerable<Participante> ListaDeEspera => _participantes
        .Where(p => p.Estado == EstadoParticipante.EnListaDeEspera)
        .OrderBy(p => p.PosicionEnListaDeEspera);

    /// <summary>Participantes que entran en el emparejamiento de la siguiente ronda.</summary>
    public IEnumerable<Participante> Activos => _participantes.Where(p => p.Activo);

    public bool Lleno => Inscritos.Count() >= Plazas;

    public Ronda? RondaActual => _rondas.FirstOrDefault(r => r.Estado == EstadoRonda.EnCurso);

    // --- Ciclo de vida ------------------------------------------------------------------------

    public void Publicar()
    {
        ExcepcionDeDominio.SiNo(Estado == EstadoTorneo.Borrador, "Solo se publica un torneo en borrador.");
        Estado = EstadoTorneo.Publicado;
    }

    public void VolverABorrador()
    {
        ExcepcionDeDominio.SiNo(Estado == EstadoTorneo.Publicado, "Solo se despublica un torneo publicado.");
        Estado = EstadoTorneo.Borrador;
    }

    public void Cancelar()
    {
        ExcepcionDeDominio.SiNo(
            Estado is EstadoTorneo.Borrador or EstadoTorneo.Publicado or EstadoTorneo.EnCurso,
            "El torneo ya estaba cerrado.");

        Estado = EstadoTorneo.Cancelado;
    }

    public void Finalizar()
    {
        ExcepcionDeDominio.SiNo(Estado == EstadoTorneo.EnCurso, "Solo se finaliza un torneo en curso.");
        ExcepcionDeDominio.SiNo(_rondas.All(r => r.Estado == EstadoRonda.Cerrada), "Hay rondas sin cerrar.");

        Estado = EstadoTorneo.Finalizado;
        Emitir(new TorneoFinalizado(TenantId, Id));
    }

    // --- Configuracion ------------------------------------------------------------------------

    /// <summary>
    /// Activa, cambia o desactiva el top cut. Permitido en cualquier momento hasta que se lanza la
    /// primera ronda, no solo al crear el torneo.
    /// </summary>
    public void ConfigurarTopCut(int? tamano)
    {
        ExcepcionDeDominio.SiNo(!ConfiguracionCongelada, "El top cut no se puede tocar con el torneo ya en curso.");

        if (tamano is { } t)
        {
            ExcepcionDeDominio.SiNo(t >= 2, "El top cut mas pequeno es de 2 jugadores.");
            ExcepcionDeDominio.SiNo((t & (t - 1)) == 0, "El top cut debe ser una potencia de dos (2, 4, 8, 16...).");
        }

        TamanoTopCut = tamano;
    }

    public void ConfigurarFase(Fase fase, ConfiguracionFase configuracion)
    {
        ExcepcionDeDominio.SiNo(!ConfiguracionCongelada, "La configuracion de fase queda congelada al empezar.");
        Configuracion.CambiarFase(fase, configuracion);
    }

    public void CambiarDatos(string nombre, DateTimeOffset comienzaEn, Direccion lugar, int plazas, int rondasSuizas)
    {
        ExcepcionDeDominio.SiNo(!ConfiguracionCongelada, "El torneo ya esta en curso.");
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "El torneo necesita un nombre.");
        ExcepcionDeDominio.SiNo(plazas >= Inscritos.Count(), "No puedes dejar menos plazas que inscritos hay.");
        ExcepcionDeDominio.SiNo(rondasSuizas >= 1, "Un torneo necesita al menos una ronda suiza.");

        Nombre = nombre.Trim();
        ComienzaEn = comienzaEn;
        Lugar = lugar;
        Plazas = plazas;
        RondasSuizas = rondasSuizas;
    }

    public void AsignarALiga(Guid? ligaId)
    {
        ExcepcionDeDominio.SiNo(!ConfiguracionCongelada, "El torneo ya esta en curso.");
        LigaId = ligaId;
    }

    // --- Participantes ------------------------------------------------------------------------

    /// <summary>
    /// Inscribe a alguien. Si el torneo esta lleno entra en lista de espera conservando el orden de
    /// llegada. El identificador de usuario es nulo en las altas manuales de la tienda.
    /// </summary>
    public Participante Inscribir(string nombre, string? usuarioId = null)
    {
        ExcepcionDeDominio.SiNo(
            Estado is EstadoTorneo.Borrador or EstadoTorneo.Publicado,
            "El torneo ya no admite inscripciones.");
        ExcepcionDeDominio.SiNo(
            usuarioId is null || _participantes.All(p => p.UsuarioId != usuarioId),
            "Ese jugador ya esta inscrito en el torneo.");

        var participante = new Participante(Id, TenantId, nombre, usuarioId);

        if (Lleno)
        {
            participante.EnviarAListaDeEspera(ListaDeEspera.Count() + 1);
        }

        _participantes.Add(participante);
        return participante;
    }

    /// <summary>
    /// Baja de un participante. Antes de empezar libera plaza y promociona al primero de la lista
    /// de espera; con el torneo en curso solo lo retira de los emparejamientos futuros.
    /// Devuelve el promocionado, si lo hubo.
    /// </summary>
    public Participante? DarDeBaja(Guid participanteId)
    {
        var participante = BuscarParticipante(participanteId);
        var liberaPlaza = participante.Estado == EstadoParticipante.Inscrito;

        participante.DarDeBaja();

        if (!liberaPlaza || Estado == EstadoTorneo.EnCurso)
        {
            return null;
        }

        var promocionado = ListaDeEspera.FirstOrDefault();
        promocionado?.Promover();

        foreach (var enEspera in ListaDeEspera)
        {
            enEspera.AdelantarEnListaDeEspera();
        }

        Emitir(new PlazaLiberada(TenantId, Id, promocionado?.Id));
        return promocionado;
    }

    public void RenombrarParticipante(Guid participanteId, string nombre) =>
        BuscarParticipante(participanteId).Renombrar(nombre);

    // --- Rondas -------------------------------------------------------------------------------

    /// <summary>
    /// Abre una ronda vacia de la fase indicada. El emparejador suizo (hito 2) la rellena llamando
    /// a Emparejar; esta operacion solo fija el reloj y el estado.
    /// </summary>
    public Ronda AbrirRonda(Fase fase, DateTimeOffset ahora)
    {
        ExcepcionDeDominio.SiNo(
            Estado is EstadoTorneo.Publicado or EstadoTorneo.EnCurso,
            "El torneo no esta en disposicion de jugar rondas.");
        ExcepcionDeDominio.SiNo(RondaActual is null, "Hay una ronda en curso sin cerrar.");
        ExcepcionDeDominio.SiNo(fase == Fase.Suiza || TieneTopCut, "Este torneo no tiene top cut configurado.");
        ExcepcionDeDominio.SiNo(
            fase != Fase.Suiza || _rondas.Count(r => r.Fase == Fase.Suiza) < RondasSuizas,
            "Ya se jugaron todas las rondas suizas previstas.");

        var ronda = new Ronda(Id, TenantId, _rondas.Count + 1, fase, Configuracion.ConfiguracionDe(fase), ahora);
        _rondas.Add(ronda);
        Estado = EstadoTorneo.EnCurso;

        Emitir(new RondaLanzada(TenantId, Id, ronda.Id, ronda.Numero, fase));
        return ronda;
    }

    public void ReportarResultado(
        Guid rondaId,
        Guid emparejamientoId,
        IReadOnlyList<GanadorPartida> partidas,
        Guid? reportadoPor)
    {
        var ronda = BuscarRonda(rondaId);
        ronda.EmparejamientoDe(emparejamientoId).Reportar(partidas, ronda.Formato, reportadoPor);
        Emitir(new ResultadoRegistrado(TenantId, Id, rondaId, emparejamientoId));
    }

    public void CorregirResultado(Guid rondaId, Guid emparejamientoId, IReadOnlyList<GanadorPartida> partidas)
    {
        var ronda = BuscarRonda(rondaId);
        ronda.EmparejamientoDe(emparejamientoId).Corregir(partidas, ronda.Formato);
        Emitir(new ResultadoRegistrado(TenantId, Id, rondaId, emparejamientoId));
    }

    /// <summary>
    /// Aplica el fin de tiempo de la ronda: lo que siga sin reportar queda en empate y se avisa
    /// para que el organizador pueda revisarlo.
    /// </summary>
    public void AplicarFinDeTiempo(Guid rondaId, DateTimeOffset ahora)
    {
        var ronda = BuscarRonda(rondaId);

        ExcepcionDeDominio.SiNo(ronda.ProrrogaAgotada(ahora), "La ronda todavia tiene tiempo.");

        var afectados = ronda.ResolverPendientesPorTiempo();

        if (afectados.Count > 0)
        {
            Emitir(new ResultadoPendiente(TenantId, Id, rondaId, [.. afectados.Select(e => e.Id)]));
        }
    }

    public void CerrarRonda(Guid rondaId, DateTimeOffset ahora) => BuscarRonda(rondaId).Cerrar(ahora);

    // --- Auxiliares ---------------------------------------------------------------------------

    private Participante BuscarParticipante(Guid participanteId) =>
        _participantes.FirstOrDefault(p => p.Id == participanteId)
        ?? throw new ExcepcionDeDominio("Ese participante no esta en el torneo.");

    private Ronda BuscarRonda(Guid rondaId) =>
        _rondas.FirstOrDefault(r => r.Id == rondaId)
        ?? throw new ExcepcionDeDominio("Esa ronda no pertenece al torneo.");
}
