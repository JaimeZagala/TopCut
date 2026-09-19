namespace TopCut.Domain.Catalogo;

/// <summary>
/// Catalogo de TCGs de la v1 y sus rulesets, como <b>datos</b>. Ninguna regla de torneo consulta
/// que juego es: activar un tercer TCG sera anadir una entrada aqui (o darla de alta desde el panel
/// de Superadmin), no escribir clases nuevas.
/// <para>
/// Los valores estan verificados contra los reglamentos oficiales vigentes citados en
/// <see cref="Ruleset.FuenteOficial"/>. Las organizaciones los revisan de vez en cuando: si cambian,
/// se corrige aqui y se actualiza <see cref="Ruleset.VersionReglas"/>.
/// </para>
/// </summary>
public static class CatalogoInicial
{
    public const string CodigoMagic = "MTG";
    public const string CodigoOnePiece = "OPTCG";

    /// <summary>Suelo que ambos juegos aplican a los porcentajes antes de promediarlos.</summary>
    private const decimal SueloPorcentaje = 0.33m;

    public static Tcg Magic() => new(CodigoMagic, "Magic: The Gathering");

    public static Tcg OnePiece() => new(CodigoOnePiece, "One Piece Card Game");

    /// <summary>
    /// Magic: The Gathering, segun las Magic Tournament Rules vigentes.
    /// <list type="bullet">
    /// <item>MTR 3.1: desempates en orden OMW%, GW%, OGW%.</item>
    /// <item>MTR apendice B: 50 minutos por ronda en Constructed y Limited; 90 en cuartos y semis.</item>
    /// <item>MTR apendice C: 3/1/0 puntos; el bye cuenta como victoria 2-0 (3 puntos de match,
    /// 6 de partida) y <b>si</b> entra en el porcentaje propio del jugador.</item>
    /// </list>
    /// </summary>
    public static Ruleset RulesetMagic(int tcgId) => new(
        tcgId: tcgId,
        puntosVictoria: 3,
        puntosEmpate: 1,
        puntosDerrota: 0,
        puntosBye: 3,
        puntosPartidaVictoria: 3,
        puntosPartidaEmpate: 1,
        puntosPartidaDerrota: 0,
        byeCuentaEnPorcentajePropio: true,
        porcentajeMinimo: SueloPorcentaje,
        faseSuiza: new ConfiguracionFase(FormatoPartida.Bo3, TimeSpan.FromMinutes(50), TimeSpan.Zero),
        faseTopCut: new ConfiguracionFase(FormatoPartida.Bo3, TimeSpan.FromMinutes(90), TimeSpan.Zero),
        criteriosDesempate:
        [
            CriterioDesempate.PorcentajeVictoriasRivales,
            CriterioDesempate.PorcentajePartidasGanadas,
            CriterioDesempate.PorcentajePartidasGanadasRivales
        ],
        versionReglas: "Magic Tournament Rules, 21 abr 2025",
        fuenteOficial: "https://media.wizards.com/ContentResources/WPN/MTG_MTR_2025_Apr%2021_EN.pdf");

    /// <summary>
    /// One Piece Card Game, segun el Bandai Organized Play Tournament Rules Manual vigente.
    /// <list type="bullet">
    /// <item>TRM 3.5: fase suiza a partida unica (30 min + 5 de prorroga) y top cut al mejor de
    /// tres (60 min + 10). La final de campeonato no tiene limite.</item>
    /// <item>TRM 5.4: 3 puntos por victoria, 3 por bye, 1 por empate, 0 por derrota. Desempate por
    /// porcentaje propio de victorias y despues por el de los rivales; suelo de 0,33.</item>
    /// <item>TRM 5.4: los byes se excluyen del porcentaje propio, <b>al contrario que en Magic</b>.
    /// Esa diferencia es la razon de que el ruleset sea configuracion y no codigo.</item>
    /// </list>
    /// </summary>
    public static Ruleset RulesetOnePiece(int tcgId) => new(
        tcgId: tcgId,
        puntosVictoria: 3,
        puntosEmpate: 1,
        puntosDerrota: 0,
        puntosBye: 3,
        puntosPartidaVictoria: 3,
        puntosPartidaEmpate: 1,
        puntosPartidaDerrota: 0,
        byeCuentaEnPorcentajePropio: false,
        porcentajeMinimo: SueloPorcentaje,
        faseSuiza: new ConfiguracionFase(FormatoPartida.Bo1, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5)),
        faseTopCut: new ConfiguracionFase(FormatoPartida.Bo3, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(10)),
        criteriosDesempate:
        [
            CriterioDesempate.PorcentajeVictoriasPropio,
            CriterioDesempate.PorcentajeVictoriasRivales,
            CriterioDesempate.EnfrentamientoDirecto
        ],
        versionReglas: "Bandai OP Tournament Rules Manual, 17 oct 2025",
        fuenteOficial: "https://en.onepiece-cardgame.com/pdf/tournament_rules_manual.pdf");
}
