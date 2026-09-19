namespace TopCut.Domain.Catalogo;

/// <summary>Fase de un torneo. Cada una tiene sus propios ajustes de formato y tiempo.</summary>
public enum Fase
{
    /// <summary>Fase de emparejamiento suizo.</summary>
    Suiza = 1,

    /// <summary>Eliminacion directa posterior. Opcional.</summary>
    TopCut = 2
}
