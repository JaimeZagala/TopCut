namespace TopCut.Domain.Torneos;

public enum EstadoTorneo
{
    /// <summary>Creado por la tienda pero aun no visible para los jugadores.</summary>
    Borrador = 1,

    /// <summary>Visible en la busqueda publica y admitiendo inscripciones.</summary>
    Publicado = 2,

    /// <summary>Primera ronda lanzada. A partir de aqui la configuracion queda congelada.</summary>
    EnCurso = 3,

    Finalizado = 4,
    Cancelado = 5
}

public enum EstadoParticipante
{
    Inscrito = 1,

    /// <summary>El torneo estaba lleno al inscribirse. Asciende solo cuando se libera una plaza.</summary>
    EnListaDeEspera = 2,

    /// <summary>
    /// Retirado a mitad de torneo: no entra en emparejamientos futuros ni recibe byes, pero sus
    /// resultados ya jugados siguen contando para la clasificacion.
    /// </summary>
    DadoDeBaja = 3
}

public enum EstadoRonda
{
    EnCurso = 1,
    Cerrada = 2
}

public enum EstadoEmparejamiento
{
    PendienteDeResultado = 1,
    Reportado = 2
}

/// <summary>Ganador de una partida individual dentro de un enfrentamiento.</summary>
public enum GanadorPartida
{
    ParticipanteA = 1,
    ParticipanteB = 2,
    Empate = 3
}
