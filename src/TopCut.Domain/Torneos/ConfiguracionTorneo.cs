using TopCut.Domain.Catalogo;
using TopCut.Domain.Common;

namespace TopCut.Domain.Torneos;

/// <summary>
/// Reglas efectivas de un torneo concreto. Es una <b>copia</b> del <see cref="Ruleset"/> del TCG
/// tomada al crear el torneo: si el Superadmin corrige despues el ruleset por defecto, los torneos
/// ya creados no cambian retroactivamente (requisito explicito en docs/vision.md).
/// </summary>
public class ConfiguracionTorneo
{
    private List<CriterioDesempate> _criteriosDesempate = [];

    private ConfiguracionTorneo() { }

    public static ConfiguracionTorneo CopiarDe(Ruleset ruleset) => new()
    {
        RulesetOrigenId = ruleset.Id,
        VersionReglas = ruleset.VersionReglas,
        PuntosVictoria = ruleset.PuntosVictoria,
        PuntosEmpate = ruleset.PuntosEmpate,
        PuntosDerrota = ruleset.PuntosDerrota,
        PuntosBye = ruleset.PuntosBye,
        PuntosPartidaVictoria = ruleset.PuntosPartidaVictoria,
        PuntosPartidaEmpate = ruleset.PuntosPartidaEmpate,
        PuntosPartidaDerrota = ruleset.PuntosPartidaDerrota,
        ByeCuentaEnPorcentajePropio = ruleset.ByeCuentaEnPorcentajePropio,
        PorcentajeMinimo = ruleset.PorcentajeMinimo,
        FaseSuiza = ruleset.FaseSuiza,
        FaseTopCut = ruleset.FaseTopCut,
        _criteriosDesempate = [.. ruleset.CriteriosDesempate]
    };

    /// <summary>Ruleset del que se copio, solo para trazabilidad. No se consulta en vivo.</summary>
    public int RulesetOrigenId { get; private set; }

    public string VersionReglas { get; private set; } = null!;

    public int PuntosVictoria { get; private set; }

    public int PuntosEmpate { get; private set; }

    public int PuntosDerrota { get; private set; }

    public int PuntosBye { get; private set; }

    public int PuntosPartidaVictoria { get; private set; }

    public int PuntosPartidaEmpate { get; private set; }

    public int PuntosPartidaDerrota { get; private set; }

    public bool ByeCuentaEnPorcentajePropio { get; private set; }

    public decimal PorcentajeMinimo { get; private set; }

    public ConfiguracionFase FaseSuiza { get; private set; }

    public ConfiguracionFase FaseTopCut { get; private set; }

    public IReadOnlyList<CriterioDesempate> CriteriosDesempate => _criteriosDesempate.AsReadOnly();

    public int PuntosPara(ResultadoEnfrentamiento resultado) => resultado switch
    {
        ResultadoEnfrentamiento.Victoria => PuntosVictoria,
        ResultadoEnfrentamiento.Empate => PuntosEmpate,
        ResultadoEnfrentamiento.Derrota => PuntosDerrota,
        ResultadoEnfrentamiento.Bye => PuntosBye,
        _ => throw new ArgumentOutOfRangeException(nameof(resultado))
    };

    public ConfiguracionFase ConfiguracionDe(Fase fase) => fase switch
    {
        Fase.Suiza => FaseSuiza,
        Fase.TopCut => FaseTopCut,
        _ => throw new ArgumentOutOfRangeException(nameof(fase))
    };

    /// <summary>Ajusta el formato o el tiempo de una fase. Solo antes de lanzar la primera ronda.</summary>
    internal void CambiarFase(Fase fase, ConfiguracionFase configuracion)
    {
        ExcepcionDeDominio.SiNo(
            configuracion.DuracionRonda is null or { TotalMinutes: > 0 },
            "La duracion de ronda debe ser positiva o nula (sin limite).");

        if (fase == Fase.Suiza)
        {
            FaseSuiza = configuracion;
        }
        else
        {
            FaseTopCut = configuracion;
        }
    }
}
