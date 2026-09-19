using TopCut.Domain.Catalogo;
using TopCut.Domain.Common;
using TopCut.Domain.Eventos;
using TopCut.Domain.Tenencia;
using TopCut.Domain.Torneos;

namespace TopCut.Domain.Tests.Torneos;

public class TorneoTests
{
    private static readonly Guid Tienda = Guid.CreateVersion7();

    private static Direccion Lugar() => new("Calle Mayor 1", "Valencia", "Valencia", "46001");

    private static Torneo CrearTorneo(int plazas = 8, int rondasSuizas = 3, Ruleset? ruleset = null) =>
        Torneo.Crear(
            Tienda,
            ruleset ?? CatalogoInicial.RulesetMagic(tcgId: 1),
            "Torneo de prueba",
            DateTimeOffset.UtcNow.AddDays(7),
            Lugar(),
            plazas,
            rondasSuizas);

    // --- Configuracion copiada del ruleset ----------------------------------------------------

    [Fact]
    public void El_torneo_copia_el_ruleset_y_no_le_afectan_los_cambios_posteriores()
    {
        var ruleset = CatalogoInicial.RulesetMagic(tcgId: 1);
        var torneo = CrearTorneo(ruleset: ruleset);

        // El Superadmin cambia el ruleset por defecto de Magic despues de crear el torneo.
        ruleset.CambiarFase(Fase.Suiza, new ConfiguracionFase(FormatoPartida.Bo1, TimeSpan.FromMinutes(20), TimeSpan.Zero));

        Assert.Equal(FormatoPartida.Bo3, torneo.Configuracion.FaseSuiza.Formato);
        Assert.Equal(TimeSpan.FromMinutes(50), torneo.Configuracion.FaseSuiza.DuracionRonda);
    }

    [Fact]
    public void La_configuracion_del_torneo_recuerda_de_que_ruleset_salio()
    {
        var ruleset = CatalogoInicial.RulesetOnePiece(tcgId: 2);
        var torneo = CrearTorneo(ruleset: ruleset);

        Assert.Equal(ruleset.Id, torneo.Configuracion.RulesetOrigenId);
        Assert.Equal(ruleset.VersionReglas, torneo.Configuracion.VersionReglas);
    }

    [Fact]
    public void Una_tienda_puede_combinar_Bo3_en_suizo_con_Bo1_en_top_cut()
    {
        var torneo = CrearTorneo();
        torneo.ConfigurarTopCut(8);

        torneo.ConfigurarFase(Fase.TopCut, new ConfiguracionFase(FormatoPartida.Bo1, TimeSpan.FromMinutes(30), TimeSpan.Zero));

        Assert.Equal(FormatoPartida.Bo3, torneo.Configuracion.FaseSuiza.Formato);
        Assert.Equal(FormatoPartida.Bo1, torneo.Configuracion.FaseTopCut.Formato);
    }

    // --- Top cut -------------------------------------------------------------------------------

    [Fact]
    public void Un_torneo_nace_sin_top_cut()
    {
        var torneo = CrearTorneo();

        Assert.False(torneo.TieneTopCut);
        Assert.Null(torneo.TamanoTopCut);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void El_top_cut_admite_cualquier_potencia_de_dos(int tamano)
    {
        var torneo = CrearTorneo();

        torneo.ConfigurarTopCut(tamano);

        Assert.Equal(tamano, torneo.TamanoTopCut);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public void El_top_cut_rechaza_tamanos_que_no_sean_potencia_de_dos(int tamano)
    {
        var torneo = CrearTorneo();

        Assert.Throws<ExcepcionDeDominio>(() => torneo.ConfigurarTopCut(tamano));
    }

    [Fact]
    public void El_top_cut_se_puede_cambiar_y_quitar_mientras_no_empiece_el_torneo()
    {
        var torneo = CrearTorneo();
        torneo.Publicar();

        torneo.ConfigurarTopCut(8);
        torneo.ConfigurarTopCut(4);
        torneo.ConfigurarTopCut(null);

        Assert.False(torneo.TieneTopCut);
    }

    [Fact]
    public void El_top_cut_queda_congelado_al_lanzar_la_primera_ronda()
    {
        var torneo = CrearTorneo();
        torneo.Publicar();
        torneo.Inscribir("Ana");
        torneo.Inscribir("Bruno");
        torneo.AbrirRonda(Fase.Suiza, DateTimeOffset.UtcNow);

        var error = Assert.Throws<ExcepcionDeDominio>(() => torneo.ConfigurarTopCut(8));

        Assert.Contains("ya en curso", error.Message);
    }

    [Fact]
    public void No_se_puede_abrir_una_ronda_de_top_cut_si_el_torneo_no_lo_tiene_configurado()
    {
        var torneo = CrearTorneo();
        torneo.Publicar();

        var error = Assert.Throws<ExcepcionDeDominio>(() => torneo.AbrirRonda(Fase.TopCut, DateTimeOffset.UtcNow));

        Assert.Contains("no tiene top cut", error.Message);
    }

    // --- Inscripciones y lista de espera --------------------------------------------------------

    [Fact]
    public void Al_llenarse_el_torneo_las_inscripciones_pasan_a_lista_de_espera_por_orden()
    {
        var torneo = CrearTorneo(plazas: 2);
        torneo.Publicar();

        torneo.Inscribir("Ana");
        torneo.Inscribir("Bruno");
        var carla = torneo.Inscribir("Carla");
        var diego = torneo.Inscribir("Diego");

        Assert.True(torneo.Lleno);
        Assert.Equal(EstadoParticipante.EnListaDeEspera, carla.Estado);
        Assert.Equal(1, carla.PosicionEnListaDeEspera);
        Assert.Equal(2, diego.PosicionEnListaDeEspera);
    }

    [Fact]
    public void Una_baja_antes_de_empezar_promociona_al_primero_de_la_lista_y_reordena_al_resto()
    {
        var torneo = CrearTorneo(plazas: 2);
        torneo.Publicar();

        var ana = torneo.Inscribir("Ana");
        torneo.Inscribir("Bruno");
        var carla = torneo.Inscribir("Carla");
        var diego = torneo.Inscribir("Diego");

        var promocionado = torneo.DarDeBaja(ana.Id);

        Assert.Same(carla, promocionado);
        Assert.Equal(EstadoParticipante.Inscrito, carla.Estado);
        Assert.Null(carla.PosicionEnListaDeEspera);
        Assert.Equal(1, diego.PosicionEnListaDeEspera);
    }

    [Fact]
    public void Una_baja_antes_de_empezar_emite_el_evento_de_plaza_liberada()
    {
        var torneo = CrearTorneo(plazas: 2);
        torneo.Publicar();
        var ana = torneo.Inscribir("Ana");
        torneo.Inscribir("Bruno");
        var carla = torneo.Inscribir("Carla");

        torneo.DarDeBaja(ana.Id);

        var evento = Assert.Single(torneo.Eventos.OfType<PlazaLiberada>());
        Assert.Equal(carla.Id, evento.PromocionadoId);
    }

    [Fact]
    public void Una_baja_a_mitad_de_torneo_no_promociona_a_nadie()
    {
        var torneo = CrearTorneo(plazas: 2);
        torneo.Publicar();
        var ana = torneo.Inscribir("Ana");
        torneo.Inscribir("Bruno");
        var carla = torneo.Inscribir("Carla");
        torneo.AbrirRonda(Fase.Suiza, DateTimeOffset.UtcNow);

        var promocionado = torneo.DarDeBaja(ana.Id);

        Assert.Null(promocionado);
        Assert.Equal(EstadoParticipante.EnListaDeEspera, carla.Estado);
    }

    [Fact]
    public void Quien_se_da_de_baja_deja_de_entrar_en_los_emparejamientos()
    {
        var torneo = CrearTorneo();
        torneo.Publicar();
        var ana = torneo.Inscribir("Ana");
        torneo.Inscribir("Bruno");
        torneo.AbrirRonda(Fase.Suiza, DateTimeOffset.UtcNow);

        torneo.DarDeBaja(ana.Id);

        Assert.DoesNotContain(ana, torneo.Activos);
        Assert.Equal(EstadoParticipante.DadoDeBaja, ana.Estado);
    }

    [Fact]
    public void Un_participante_sin_cuenta_es_valido()
    {
        var torneo = CrearTorneo();

        var participante = torneo.Inscribir("Jugador de mostrador");

        Assert.Null(participante.UsuarioId);
        Assert.Equal(EstadoParticipante.Inscrito, participante.Estado);
    }

    [Fact]
    public void El_mismo_usuario_no_se_puede_inscribir_dos_veces()
    {
        var torneo = CrearTorneo();
        torneo.Inscribir("Ana", usuarioId: "usuario-1");

        Assert.Throws<ExcepcionDeDominio>(() => torneo.Inscribir("Ana otra vez", usuarioId: "usuario-1"));
    }

    [Fact]
    public void Un_torneo_en_curso_no_admite_inscripciones()
    {
        var torneo = CrearTorneo();
        torneo.Publicar();
        torneo.Inscribir("Ana");
        torneo.AbrirRonda(Fase.Suiza, DateTimeOffset.UtcNow);

        Assert.Throws<ExcepcionDeDominio>(() => torneo.Inscribir("Tarde"));
    }

    // --- Rondas ----------------------------------------------------------------------------------

    [Fact]
    public void Abrir_una_ronda_emite_el_evento_y_pone_el_torneo_en_curso()
    {
        var torneo = CrearTorneo();
        torneo.Publicar();

        var ronda = torneo.AbrirRonda(Fase.Suiza, DateTimeOffset.UtcNow);

        Assert.Equal(EstadoTorneo.EnCurso, torneo.Estado);
        Assert.Equal(1, ronda.Numero);
        var evento = Assert.Single(torneo.Eventos.OfType<RondaLanzada>());
        Assert.Equal(ronda.Id, evento.RondaId);
    }

    [Fact]
    public void No_se_puede_abrir_una_ronda_con_otra_sin_cerrar()
    {
        var torneo = CrearTorneo();
        torneo.Publicar();
        torneo.AbrirRonda(Fase.Suiza, DateTimeOffset.UtcNow);

        var error = Assert.Throws<ExcepcionDeDominio>(() => torneo.AbrirRonda(Fase.Suiza, DateTimeOffset.UtcNow));

        Assert.Contains("ronda en curso", error.Message);
    }

    [Fact]
    public void No_se_pueden_abrir_mas_rondas_suizas_de_las_previstas()
    {
        var torneo = CrearTorneo(rondasSuizas: 1);
        torneo.Publicar();
        var ahora = DateTimeOffset.UtcNow;
        var ronda = torneo.AbrirRonda(Fase.Suiza, ahora);
        torneo.CerrarRonda(ronda.Id, ahora);

        var error = Assert.Throws<ExcepcionDeDominio>(() => torneo.AbrirRonda(Fase.Suiza, ahora));

        Assert.Contains("todas las rondas suizas", error.Message);
    }

    [Fact]
    public void Un_torneo_solo_se_finaliza_con_todas_las_rondas_cerradas()
    {
        var torneo = CrearTorneo(rondasSuizas: 1);
        torneo.Publicar();
        var ronda = torneo.AbrirRonda(Fase.Suiza, DateTimeOffset.UtcNow);

        Assert.Throws<ExcepcionDeDominio>(torneo.Finalizar);

        torneo.CerrarRonda(ronda.Id, DateTimeOffset.UtcNow);
        torneo.Finalizar();

        Assert.Equal(EstadoTorneo.Finalizado, torneo.Estado);
        Assert.Single(torneo.Eventos.OfType<TorneoFinalizado>());
    }
}
