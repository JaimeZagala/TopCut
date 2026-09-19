using TopCut.Domain.Catalogo;
using TopCut.Domain.Common;
using TopCut.Domain.Eventos;
using TopCut.Domain.Tenencia;
using TopCut.Domain.Torneos;

namespace TopCut.Domain.Tests.Torneos;

public class RondaYEmparejamientoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 19, 17, 0, 0, TimeSpan.Zero);

    private static Torneo TorneoDe(Ruleset ruleset) => Torneo.Crear(
        Guid.CreateVersion7(),
        ruleset,
        "Torneo de prueba",
        Ahora.AddDays(1),
        new Direccion("Calle Mayor 1", "Valencia", "Valencia", "46001"),
        plazas: 16,
        rondasSuizas: 4);

    private static Torneo Magic() => TorneoDe(CatalogoInicial.RulesetMagic(tcgId: 1));

    private static Torneo OnePiece() => TorneoDe(CatalogoInicial.RulesetOnePiece(tcgId: 2));

    // --- Cronometro -------------------------------------------------------------------------

    [Fact]
    public void La_ronda_guarda_el_instante_de_fin_y_no_los_segundos_restantes()
    {
        var torneo = Magic();
        torneo.Publicar();

        var ronda = torneo.AbrirRonda(Fase.Suiza, Ahora);

        Assert.Equal(Ahora.AddMinutes(50), ronda.FinTiempoRegularEn);
        Assert.Equal(TimeSpan.FromMinutes(30), ronda.TiempoRestante(Ahora.AddMinutes(20)));
    }

    [Fact]
    public void OnePiece_suma_la_prorroga_al_instante_limite_de_la_ronda()
    {
        var torneo = OnePiece();
        torneo.Publicar();

        var ronda = torneo.AbrirRonda(Fase.Suiza, Ahora);

        Assert.Equal(Ahora.AddMinutes(30), ronda.FinTiempoRegularEn);
        Assert.Equal(Ahora.AddMinutes(35), ronda.FinConProrrogaEn);
    }

    [Fact]
    public void El_tiempo_restante_nunca_es_negativo()
    {
        var torneo = Magic();
        torneo.Publicar();
        var ronda = torneo.AbrirRonda(Fase.Suiza, Ahora);

        Assert.Equal(TimeSpan.Zero, ronda.TiempoRestante(Ahora.AddHours(3)));
    }

    // --- Byes -------------------------------------------------------------------------------

    [Fact]
    public void Un_bye_nace_ya_resuelto_como_victoria_automatica()
    {
        var torneo = Magic();
        torneo.Publicar();
        var ana = torneo.Inscribir("Ana");
        var ronda = torneo.AbrirRonda(Fase.Suiza, Ahora);

        var bye = ronda.Emparejar(mesa: 1, ana.Id, participanteBId: null);

        Assert.True(bye.EsBye);
        Assert.Equal(EstadoEmparejamiento.Reportado, bye.Estado);
        Assert.Equal(ResultadoEnfrentamiento.Bye, bye.ResultadoDe(ana.Id));
    }

    [Fact]
    public void Un_bye_no_admite_que_se_le_reporte_un_resultado()
    {
        var torneo = Magic();
        torneo.Publicar();
        var ana = torneo.Inscribir("Ana");
        var ronda = torneo.AbrirRonda(Fase.Suiza, Ahora);
        var bye = ronda.Emparejar(mesa: 1, ana.Id, participanteBId: null);

        Assert.Throws<ExcepcionDeDominio>(() => torneo.ReportarResultado(
            ronda.Id, bye.Id, [GanadorPartida.ParticipanteA], reportadoPor: null));
    }

    [Fact]
    public void Nadie_se_enfrenta_a_si_mismo()
    {
        var torneo = Magic();
        torneo.Publicar();
        var ana = torneo.Inscribir("Ana");
        var ronda = torneo.AbrirRonda(Fase.Suiza, Ahora);

        Assert.Throws<ExcepcionDeDominio>(() => ronda.Emparejar(mesa: 1, ana.Id, ana.Id));
    }

    // --- Reporte de resultados ------------------------------------------------------------------

    [Fact]
    public void En_Bo3_se_guarda_cada_partida_y_no_solo_el_global()
    {
        var (torneo, ronda, emparejamiento, ana, bruno) = PartidaEnCurso(Magic());

        torneo.ReportarResultado(
            ronda.Id,
            emparejamiento.Id,
            [GanadorPartida.ParticipanteA, GanadorPartida.ParticipanteB, GanadorPartida.ParticipanteA],
            reportadoPor: ana.Id);

        Assert.Equal(3, emparejamiento.Partidas.Count);
        Assert.Equal([1, 2, 3], emparejamiento.Partidas.Select(p => p.Numero));
        Assert.Equal(2, emparejamiento.PartidasGanadasDe(ana.Id));
        Assert.Equal(1, emparejamiento.PartidasGanadasDe(bruno.Id));
        Assert.Equal(ResultadoEnfrentamiento.Victoria, emparejamiento.ResultadoDe(ana.Id));
        Assert.Equal(ResultadoEnfrentamiento.Derrota, emparejamiento.ResultadoDe(bruno.Id));
    }

    [Fact]
    public void En_Bo1_una_sola_partida_decide_el_enfrentamiento()
    {
        var (torneo, ronda, emparejamiento, ana, bruno) = PartidaEnCurso(OnePiece());

        torneo.ReportarResultado(ronda.Id, emparejamiento.Id, [GanadorPartida.ParticipanteB], ana.Id);

        Assert.Equal(ResultadoEnfrentamiento.Victoria, emparejamiento.ResultadoDe(bruno.Id));
        Assert.Equal(ResultadoEnfrentamiento.Derrota, emparejamiento.ResultadoDe(ana.Id));
    }

    [Fact]
    public void En_Bo1_no_caben_dos_partidas()
    {
        var (torneo, ronda, emparejamiento, ana, _) = PartidaEnCurso(OnePiece());

        var error = Assert.Throws<ExcepcionDeDominio>(() => torneo.ReportarResultado(
            ronda.Id, emparejamiento.Id, [GanadorPartida.ParticipanteA, GanadorPartida.ParticipanteB], ana.Id));

        Assert.Contains("no admite mas de 1", error.Message);
    }

    [Fact]
    public void En_Bo3_no_se_juegan_partidas_despues_de_estar_decidido_el_enfrentamiento()
    {
        var (torneo, ronda, emparejamiento, ana, _) = PartidaEnCurso(Magic());

        // Ana gana las dos primeras: la tercera no llego a jugarse nunca.
        var error = Assert.Throws<ExcepcionDeDominio>(() => torneo.ReportarResultado(
            ronda.Id,
            emparejamiento.Id,
            [
                GanadorPartida.ParticipanteA,
                GanadorPartida.ParticipanteA,
                GanadorPartida.ParticipanteB
            ],
            ana.Id));

        Assert.Contains("ya estaba decidido", error.Message);
    }

    [Fact]
    public void En_Bo3_un_dos_a_cero_es_un_reporte_valido_de_dos_partidas()
    {
        var (torneo, ronda, emparejamiento, ana, bruno) = PartidaEnCurso(Magic());

        torneo.ReportarResultado(
            ronda.Id,
            emparejamiento.Id,
            [GanadorPartida.ParticipanteA, GanadorPartida.ParticipanteA],
            ana.Id);

        Assert.Equal(2, emparejamiento.Partidas.Count);
        Assert.Equal(ResultadoEnfrentamiento.Victoria, emparejamiento.ResultadoDe(ana.Id));
        Assert.Equal(0, emparejamiento.PartidasGanadasDe(bruno.Id));
    }

    [Fact]
    public void Un_Bo3_que_acaba_uno_a_uno_con_empate_es_empate()
    {
        var (torneo, ronda, emparejamiento, ana, bruno) = PartidaEnCurso(Magic());

        torneo.ReportarResultado(
            ronda.Id,
            emparejamiento.Id,
            [GanadorPartida.ParticipanteA, GanadorPartida.ParticipanteB, GanadorPartida.Empate],
            ana.Id);

        Assert.Equal(ResultadoEnfrentamiento.Empate, emparejamiento.ResultadoDe(ana.Id));
        Assert.Equal(ResultadoEnfrentamiento.Empate, emparejamiento.ResultadoDe(bruno.Id));
    }

    [Fact]
    public void La_tienda_puede_corregir_un_resultado_y_queda_marcado()
    {
        var (torneo, ronda, emparejamiento, ana, bruno) = PartidaEnCurso(OnePiece());
        torneo.ReportarResultado(ronda.Id, emparejamiento.Id, [GanadorPartida.ParticipanteA], ana.Id);

        torneo.CorregirResultado(ronda.Id, emparejamiento.Id, [GanadorPartida.ParticipanteB]);

        Assert.True(emparejamiento.CorregidoPorOrganizador);
        Assert.Null(emparejamiento.ReportadoPor);
        Assert.Equal(ResultadoEnfrentamiento.Victoria, emparejamiento.ResultadoDe(bruno.Id));
    }

    [Fact]
    public void Un_emparejamiento_sin_resultado_no_se_puede_consultar()
    {
        var (_, _, emparejamiento, ana, _) = PartidaEnCurso(Magic());

        var error = Assert.Throws<ExcepcionDeDominio>(() => emparejamiento.ResultadoDe(ana.Id));

        Assert.Contains("aun no tiene resultado", error.Message);
    }

    // --- Fin de tiempo -----------------------------------------------------------------------------

    [Fact]
    public void Al_agotarse_la_prorroga_lo_que_quede_sin_reportar_pasa_a_empate()
    {
        var (torneo, ronda, emparejamiento, ana, bruno) = PartidaEnCurso(OnePiece());

        torneo.AplicarFinDeTiempo(ronda.Id, Ahora.AddMinutes(35));

        Assert.True(emparejamiento.EmpatePorTiempoAgotado);
        Assert.Equal(ResultadoEnfrentamiento.Empate, emparejamiento.ResultadoDe(ana.Id));
        Assert.Equal(ResultadoEnfrentamiento.Empate, emparejamiento.ResultadoDe(bruno.Id));
    }

    [Fact]
    public void El_fin_de_tiempo_avisa_de_los_enfrentamientos_afectados()
    {
        var (torneo, ronda, emparejamiento, _, _) = PartidaEnCurso(OnePiece());

        torneo.AplicarFinDeTiempo(ronda.Id, Ahora.AddMinutes(35));

        var evento = Assert.Single(torneo.Eventos.OfType<ResultadoPendiente>());
        Assert.Equal([emparejamiento.Id], evento.EmparejamientosAfectados);
    }

    [Fact]
    public void No_se_puede_aplicar_el_fin_de_tiempo_durante_la_prorroga()
    {
        var (torneo, ronda, _, _, _) = PartidaEnCurso(OnePiece());

        // A los 32 minutos el tiempo regular ya expiro, pero la prorroga de 5 minutos sigue viva.
        var error = Assert.Throws<ExcepcionDeDominio>(
            () => torneo.AplicarFinDeTiempo(ronda.Id, Ahora.AddMinutes(32)));

        Assert.Contains("todavia tiene tiempo", error.Message);
    }

    [Fact]
    public void Un_resultado_ya_reportado_no_se_pisa_al_agotarse_el_tiempo()
    {
        var (torneo, ronda, emparejamiento, ana, _) = PartidaEnCurso(OnePiece());
        torneo.ReportarResultado(ronda.Id, emparejamiento.Id, [GanadorPartida.ParticipanteA], ana.Id);

        torneo.AplicarFinDeTiempo(ronda.Id, Ahora.AddMinutes(35));

        Assert.False(emparejamiento.EmpatePorTiempoAgotado);
        Assert.Equal(ResultadoEnfrentamiento.Victoria, emparejamiento.ResultadoDe(ana.Id));
    }

    [Fact]
    public void Una_ronda_con_resultados_pendientes_no_se_puede_cerrar()
    {
        var (torneo, ronda, _, _, _) = PartidaEnCurso(Magic());

        var error = Assert.Throws<ExcepcionDeDominio>(() => torneo.CerrarRonda(ronda.Id, Ahora.AddMinutes(50)));

        Assert.Contains("resultados pendientes", error.Message);
    }

    private static (Torneo Torneo, Ronda Ronda, Emparejamiento Emparejamiento, Participante Ana, Participante Bruno)
        PartidaEnCurso(Torneo torneo)
    {
        torneo.Publicar();
        var ana = torneo.Inscribir("Ana");
        var bruno = torneo.Inscribir("Bruno");
        var ronda = torneo.AbrirRonda(Fase.Suiza, Ahora);
        var emparejamiento = ronda.Emparejar(mesa: 1, ana.Id, bruno.Id);

        return (torneo, ronda, emparejamiento, ana, bruno);
    }
}
