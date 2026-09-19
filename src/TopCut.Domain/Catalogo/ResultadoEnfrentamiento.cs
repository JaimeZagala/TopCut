namespace TopCut.Domain.Catalogo;

/// <summary>Resultado de un enfrentamiento desde el punto de vista de uno de los dos jugadores.</summary>
public enum ResultadoEnfrentamiento
{
    Victoria = 1,
    Empate = 2,
    Derrota = 3,

    /// <summary>
    /// Victoria automatica por numero impar de jugadores. Se distingue de una victoria normal
    /// porque los desempates la tratan aparte.
    /// </summary>
    Bye = 4
}
