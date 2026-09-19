namespace TopCut.Domain.Common;

/// <summary>
/// Marca una entidad como propiedad de una tienda. El filtro global de EF Core
/// (<c>HasQueryFilter</c>) se apoya en esta interfaz: ningun caso de uso filtra el tenant a mano.
/// </summary>
public interface IPerteneceATenant
{
    Guid TenantId { get; }
}

/// <summary>Fechas de creacion y ultima modificacion, rellenadas por el interceptor de EF Core.</summary>
public interface IAuditable
{
    DateTimeOffset CreadoEn { get; set; }
    DateTimeOffset? ModificadoEn { get; set; }
}

/// <summary>
/// Control de concurrencia optimista. Lo implementan las entidades donde dos acciones simultaneas
/// pueden pisarse: reportar un resultado, ocupar la ultima plaza, promover desde la lista de espera.
/// </summary>
public interface IConControlDeConcurrencia
{
    byte[]? RowVersion { get; set; }
}
