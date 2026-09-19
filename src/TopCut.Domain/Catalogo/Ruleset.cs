using TopCut.Domain.Common;

namespace TopCut.Domain.Catalogo;

/// <summary>
/// Reglas de competicion de un TCG, como datos de configuracion y no como codigo especifico por
/// juego. Un torneo <b>copia</b> estos valores al crearse, de modo que cambiar el ruleset no altera
/// torneos ya creados (ver <c>Torneos.ConfiguracionTorneo</c>).
/// </summary>
public class Ruleset : EntidadDeCatalogo
{
    private List<CriterioDesempate> _criteriosDesempate = [];

    private Ruleset() { }

    public Ruleset(
        int tcgId,
        int puntosVictoria,
        int puntosEmpate,
        int puntosDerrota,
        int puntosBye,
        int puntosPartidaVictoria,
        int puntosPartidaEmpate,
        int puntosPartidaDerrota,
        bool byeCuentaEnPorcentajePropio,
        decimal porcentajeMinimo,
        ConfiguracionFase faseSuiza,
        ConfiguracionFase faseTopCut,
        IEnumerable<CriterioDesempate> criteriosDesempate,
        string versionReglas,
        string fuenteOficial)
    {
        var criterios = criteriosDesempate.ToList();

        ExcepcionDeDominio.SiNo(criterios.Count > 0, "Un ruleset necesita al menos un criterio de desempate.");
        ExcepcionDeDominio.SiNo(
            criterios.Distinct().Count() == criterios.Count,
            "Los criterios de desempate no pueden repetirse.");
        ExcepcionDeDominio.SiNo(
            porcentajeMinimo is >= 0m and <= 1m,
            "El porcentaje minimo debe estar entre 0 y 1.");
        ExcepcionDeDominio.SiNo(
            puntosVictoria >= puntosEmpate && puntosEmpate >= puntosDerrota,
            "Los puntos deben ser decrecientes: victoria >= empate >= derrota.");

        TcgId = tcgId;
        PuntosVictoria = puntosVictoria;
        PuntosEmpate = puntosEmpate;
        PuntosDerrota = puntosDerrota;
        PuntosBye = puntosBye;
        PuntosPartidaVictoria = puntosPartidaVictoria;
        PuntosPartidaEmpate = puntosPartidaEmpate;
        PuntosPartidaDerrota = puntosPartidaDerrota;
        ByeCuentaEnPorcentajePropio = byeCuentaEnPorcentajePropio;
        PorcentajeMinimo = porcentajeMinimo;
        FaseSuiza = faseSuiza;
        FaseTopCut = faseTopCut;
        _criteriosDesempate = criterios;
        VersionReglas = versionReglas;
        FuenteOficial = fuenteOficial;
    }

    public int TcgId { get; private set; }

    public Tcg Tcg { get; private set; } = null!;

    // --- Puntuacion del enfrentamiento -------------------------------------------------------

    public int PuntosVictoria { get; private set; }

    public int PuntosEmpate { get; private set; }

    public int PuntosDerrota { get; private set; }

    /// <summary>Puntos que otorga un bye. En Magic y One Piece equivalen a una victoria.</summary>
    public int PuntosBye { get; private set; }

    // --- Puntuacion de la partida individual (solo la usan los desempates por partidas) -------

    public int PuntosPartidaVictoria { get; private set; }

    public int PuntosPartidaEmpate { get; private set; }

    public int PuntosPartidaDerrota { get; private set; }

    // --- Desempates ---------------------------------------------------------------------------

    /// <summary>
    /// Si los byes del propio jugador entran en su porcentaje de victorias. <b>Aqui los dos juegos
    /// difieren</b>: Magic los incluye (un bye es una victoria 2-0 a todos los efectos) y One Piece
    /// los excluye tanto del numerador como del denominador. Es la razon de que este flag exista.
    /// </summary>
    public bool ByeCuentaEnPorcentajePropio { get; private set; }

    /// <summary>
    /// Suelo aplicado a cualquier porcentaje antes de promediarlo, para que una mala racha de un
    /// rival no hunda el desempate de los demas. Ambos juegos usan 0,33.
    /// </summary>
    public decimal PorcentajeMinimo { get; private set; }

    /// <summary>Criterios de desempate <b>en orden de aplicacion</b>. El orden es significativo.</summary>
    public IReadOnlyList<CriterioDesempate> CriteriosDesempate => _criteriosDesempate.AsReadOnly();

    // --- Fases ---------------------------------------------------------------------------------

    public ConfiguracionFase FaseSuiza { get; private set; }

    public ConfiguracionFase FaseTopCut { get; private set; }

    // --- Trazabilidad ---------------------------------------------------------------------------

    /// <summary>Version del reglamento oficial contra la que se validaron estos valores.</summary>
    public string VersionReglas { get; private set; } = null!;

    /// <summary>URL del documento oficial, para poder reverificar cuando la organizacion lo revise.</summary>
    public string FuenteOficial { get; private set; } = null!;

    /// <summary>Puntos que corresponden a un resultado de enfrentamiento.</summary>
    public int PuntosPara(ResultadoEnfrentamiento resultado) => resultado switch
    {
        ResultadoEnfrentamiento.Victoria => PuntosVictoria,
        ResultadoEnfrentamiento.Empate => PuntosEmpate,
        ResultadoEnfrentamiento.Derrota => PuntosDerrota,
        ResultadoEnfrentamiento.Bye => PuntosBye,
        _ => throw new ArgumentOutOfRangeException(nameof(resultado))
    };

    /// <summary>Ajustes de la fase indicada.</summary>
    public ConfiguracionFase ConfiguracionDe(Fase fase) => fase switch
    {
        Fase.Suiza => FaseSuiza,
        Fase.TopCut => FaseTopCut,
        _ => throw new ArgumentOutOfRangeException(nameof(fase))
    };

    /// <summary>
    /// Ajusta el ruleset por defecto de un TCG (panel de Superadmin). Solo afecta a los torneos que
    /// se creen a partir de ahora: los existentes conservan la copia que hicieron al nacer.
    /// </summary>
    public void CambiarFase(Fase fase, ConfiguracionFase configuracion)
    {
        if (fase == Fase.Suiza)
        {
            FaseSuiza = configuracion;
        }
        else
        {
            FaseTopCut = configuracion;
        }
    }

    /// <summary>Reordena o sustituye los criterios de desempate del TCG.</summary>
    public void CambiarCriteriosDesempate(IEnumerable<CriterioDesempate> criterios)
    {
        var lista = criterios.ToList();

        ExcepcionDeDominio.SiNo(lista.Count > 0, "Un ruleset necesita al menos un criterio de desempate.");
        ExcepcionDeDominio.SiNo(
            lista.Distinct().Count() == lista.Count,
            "Los criterios de desempate no pueden repetirse.");

        _criteriosDesempate = lista;
    }

    /// <summary>Deja constancia de contra que version del reglamento oficial se reviso.</summary>
    public void MarcarRevisadoContra(string versionReglas, string fuenteOficial)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(versionReglas), "Indica la version del reglamento.");
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(fuenteOficial), "Indica la fuente oficial.");

        VersionReglas = versionReglas;
        FuenteOficial = fuenteOficial;
    }
}
