using TopCut.Domain.Catalogo;
using TopCut.Domain.Common;

namespace TopCut.Domain.Tests.Catalogo;

/// <summary>
/// Blinda los valores del seed contra las reglas oficiales citadas en cada ruleset. Si alguna
/// organizacion revisa su reglamento, estos tests son los que deben fallar primero.
/// </summary>
public class RulesetsOficialesTests
{
    // Instancias nuevas en cada acceso: ningun test puede contaminar a otro.
    private static Ruleset Magic => CatalogoInicial.RulesetMagic(tcgId: 1);

    private static Ruleset OnePiece => CatalogoInicial.RulesetOnePiece(tcgId: 2);

    [Fact]
    public void Magic_puntua_3_1_0_y_el_bye_vale_como_victoria()
    {
        Assert.Equal(3, Magic.PuntosPara(ResultadoEnfrentamiento.Victoria));
        Assert.Equal(1, Magic.PuntosPara(ResultadoEnfrentamiento.Empate));
        Assert.Equal(0, Magic.PuntosPara(ResultadoEnfrentamiento.Derrota));
        Assert.Equal(
            Magic.PuntosPara(ResultadoEnfrentamiento.Victoria),
            Magic.PuntosPara(ResultadoEnfrentamiento.Bye));
    }

    [Fact]
    public void Magic_aplica_los_tres_desempates_del_MTR_en_orden()
    {
        Assert.Equal(
            [
                CriterioDesempate.PorcentajeVictoriasRivales,
                CriterioDesempate.PorcentajePartidasGanadas,
                CriterioDesempate.PorcentajePartidasGanadasRivales
            ],
            Magic.CriteriosDesempate);
    }

    [Fact]
    public void Magic_juega_las_dos_fases_al_mejor_de_tres()
    {
        Assert.Equal(FormatoPartida.Bo3, Magic.FaseSuiza.Formato);
        Assert.Equal(FormatoPartida.Bo3, Magic.FaseTopCut.Formato);
    }

    [Fact]
    public void Magic_usa_50_minutos_en_suizo_y_90_en_top_cut_sin_prorroga_de_reloj()
    {
        Assert.Equal(TimeSpan.FromMinutes(50), Magic.FaseSuiza.DuracionRonda);
        Assert.Equal(TimeSpan.FromMinutes(90), Magic.FaseTopCut.DuracionRonda);
        Assert.Equal(TimeSpan.Zero, Magic.FaseSuiza.TiempoExtra);
    }

    [Fact]
    public void OnePiece_juega_suizo_a_partida_unica_y_top_cut_al_mejor_de_tres()
    {
        Assert.Equal(FormatoPartida.Bo1, OnePiece.FaseSuiza.Formato);
        Assert.Equal(FormatoPartida.Bo3, OnePiece.FaseTopCut.Formato);
    }

    [Fact]
    public void OnePiece_anade_prorroga_de_reloj_a_las_dos_fases()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), OnePiece.FaseSuiza.DuracionRonda);
        Assert.Equal(TimeSpan.FromMinutes(5), OnePiece.FaseSuiza.TiempoExtra);
        Assert.Equal(TimeSpan.FromMinutes(35), OnePiece.FaseSuiza.DuracionTotal);

        Assert.Equal(TimeSpan.FromMinutes(60), OnePiece.FaseTopCut.DuracionRonda);
        Assert.Equal(TimeSpan.FromMinutes(10), OnePiece.FaseTopCut.TiempoExtra);
        Assert.Equal(TimeSpan.FromMinutes(70), OnePiece.FaseTopCut.DuracionTotal);
    }

    [Fact]
    public void OnePiece_desempata_primero_por_el_porcentaje_propio()
    {
        Assert.Equal(
            [
                CriterioDesempate.PorcentajeVictoriasPropio,
                CriterioDesempate.PorcentajeVictoriasRivales,
                CriterioDesempate.EnfrentamientoDirecto
            ],
            OnePiece.CriteriosDesempate);

        // Su fase suiza es a partida unica, asi que no puede desempatar por partidas ganadas.
        Assert.DoesNotContain(CriterioDesempate.PorcentajePartidasGanadas, OnePiece.CriteriosDesempate);
    }

    [Fact]
    public void Los_dos_juegos_tratan_el_bye_de_forma_distinta_en_el_porcentaje_propio()
    {
        // Es la diferencia que justifica que el ruleset sea configuracion y no codigo.
        Assert.True(Magic.ByeCuentaEnPorcentajePropio);
        Assert.False(OnePiece.ByeCuentaEnPorcentajePropio);
    }

    [Fact]
    public void Ambos_juegos_aplican_el_suelo_de_033_a_los_porcentajes()
    {
        Assert.Equal(0.33m, Magic.PorcentajeMinimo);
        Assert.Equal(0.33m, OnePiece.PorcentajeMinimo);
    }

    [Fact]
    public void Cada_ruleset_cita_la_version_del_reglamento_contra_la_que_se_valido()
    {
        foreach (var ruleset in new[] { Magic, OnePiece })
        {
            Assert.False(string.IsNullOrWhiteSpace(ruleset.VersionReglas));
            Assert.StartsWith("https://", ruleset.FuenteOficial);
        }
    }

    [Fact]
    public void Un_ruleset_no_admite_criterios_de_desempate_repetidos()
    {
        var error = Assert.Throws<ExcepcionDeDominio>(() => OnePiece.CambiarCriteriosDesempate(
        [
            CriterioDesempate.PorcentajeVictoriasRivales,
            CriterioDesempate.PorcentajeVictoriasRivales
        ]));

        Assert.Contains("no pueden repetirse", error.Message);
    }
}
