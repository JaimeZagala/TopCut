using TopCut.Domain.Common;

namespace TopCut.Domain.Torneos;

/// <summary>
/// Jugador dentro de un torneo concreto. <b>No presupone cuenta de usuario</b>: la tienda da de
/// alta a mano a quien se inscribe en persona, y <see cref="UsuarioId"/> queda vacio.
/// </summary>
public class Participante : EntidadDeNegocio, IPerteneceATenant, IConControlDeConcurrencia
{
    private Participante() { }

    internal Participante(Guid torneoId, Guid tenantId, string nombre, string? usuarioId)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "El participante necesita un nombre.");

        TorneoId = torneoId;
        TenantId = tenantId;
        Nombre = nombre.Trim();
        UsuarioId = usuarioId;
        Estado = EstadoParticipante.Inscrito;
        InscritoEn = DateTimeOffset.UtcNow;
    }

    public Guid TorneoId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>
    /// Nombre con el que aparece en emparejamientos y clasificacion. Se guarda aunque haya cuenta
    /// asociada, para que el organizador pueda imprimir las mesas sin cargar los usuarios.
    /// </summary>
    public string Nombre { get; private set; } = null!;

    /// <summary>Usuario de Identity, si el jugador se inscribio desde la aplicacion.</summary>
    public string? UsuarioId { get; private set; }

    public EstadoParticipante Estado { get; private set; }

    /// <summary>Orden en la lista de espera. <c>null</c> si no esta en ella.</summary>
    public int? PosicionEnListaDeEspera { get; private set; }

    public DateTimeOffset InscritoEn { get; private set; }

    public byte[]? RowVersion { get; set; }

    /// <summary>Cuenta para emparejar la siguiente ronda.</summary>
    public bool Activo => Estado == EstadoParticipante.Inscrito;

    internal void EnviarAListaDeEspera(int posicion)
    {
        Estado = EstadoParticipante.EnListaDeEspera;
        PosicionEnListaDeEspera = posicion;
    }

    internal void Promover()
    {
        ExcepcionDeDominio.SiNo(
            Estado == EstadoParticipante.EnListaDeEspera,
            "Solo se promociona a quien esta en lista de espera.");

        Estado = EstadoParticipante.Inscrito;
        PosicionEnListaDeEspera = null;
    }

    internal void AdelantarEnListaDeEspera()
    {
        if (PosicionEnListaDeEspera is > 1)
        {
            PosicionEnListaDeEspera--;
        }
    }

    /// <summary>
    /// Retirada. Conserva los resultados ya jugados y deja de entrar en emparejamientos y byes.
    /// </summary>
    internal void DarDeBaja()
    {
        ExcepcionDeDominio.SiNo(
            Estado != EstadoParticipante.DadoDeBaja,
            "El participante ya estaba dado de baja.");

        Estado = EstadoParticipante.DadoDeBaja;
        PosicionEnListaDeEspera = null;
    }

    internal void Renombrar(string nombre)
    {
        ExcepcionDeDominio.SiNo(!string.IsNullOrWhiteSpace(nombre), "El participante necesita un nombre.");
        Nombre = nombre.Trim();
    }
}
