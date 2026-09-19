# Vision y alcance

> Ultima actualizacion: 2026-09-19 (hito 0)

## El problema

Las tiendas locales que organizan torneos de TCG gestionan inscripciones, emparejamientos y
resultados con hojas de calculo, papel o herramientas genericas que no conocen las reglas
especificas de cada juego (formato de partida, sistema de puntos, desempates). Eso ralentiza
los eventos y genera errores.

## La solucion

Una plataforma **multi-tenant** donde cada tienda gestiona sus propios torneos y ligas, con un
motor de emparejamiento suizo y reglas configurables por TCG, mientras los jugadores buscan
eventos, se inscriben y consultan resultados desde el navegador.

## Alcance de la v1 (MVP)

Uso real por **una unica tienda** (la de Jaime), pero con arquitectura multi-tenant desde el
primer commit: nada asume que solo existe una tienda.

Incluido:

- Dos TCG activos: Magic: The Gathering y One Piece Card Game (catalogo extensible por Superadmin).
- Solo aplicacion web (responsive, usable desde el navegador del movil). Sin app nativa.
- Torneos puntuales y ligas por temporada, con fase suiza y **top cut opcional y configurable**.
- Busqueda de eventos por ciudad / tienda / TCG / fecha (sin geolocalizacion por GPS).
- Lista de espera con promocion automatica al liberarse una plaza.
- Registro con email/contrasena y con Google.
- Cronometro de ronda sincronizado por SignalR, visible para jugadores y organizador.
- Aviso basico de proteccion de datos (RGPD) en el registro.

No incluido en la v1:

- Notificaciones push (movil o Web Push).
- Geolocalizacion real ("torneos cerca de mi").
- App Android nativa.
- Branding por tienda.

Ver [`roadmap.md`](roadmap.md) para el estado de cada fase y [`arquitectura.md`](arquitectura.md)
para las decisiones que dejan estas piezas faciles de anadir despues.

## Roles

| Rol | Capacidades |
|---|---|
| **Superadmin** | Configuracion global (tiempos de ronda por defecto, activar/desactivar TCGs del catalogo). Aprobar o revocar el rol de tienda de una cuenta. Vision y soporte transversal sobre todas las tiendas y torneos. |
| **Tienda local** | Crear, programar y publicar torneos y ligas. Configurar cada torneo segun el TCG. Dar de alta jugadores manualmente. Lanzar rondas, ver emparejamientos en vivo, corregir resultados. Gestionar lista de espera y cerrar torneos. |
| **Jugador** | Registro libre, sin aprobacion. Buscar torneos, inscribirse o desapuntarse (con paso por lista de espera). Reportar su propio resultado. Consultar historial y clasificacion en ligas activas. |

Regla clave: **cualquiera puede registrarse, pero una cuenta no puede crear ni publicar torneos
hasta que un Superadmin la active como tienda** (control de costes de infraestructura).

Los tres roles se modelan con ASP.NET Core Identity (roles + politicas de autorizacion), no con
logica ad-hoc dispersa por el codigo.

## Reglas de torneo

### Emparejamiento

Sistema **suizo, variante "Dutch"** (estandar de facto en Magic, One Piece, Pokemon y la mayoria
de TCGs organizados):

- Agrupar jugadores por puntuacion (record) y emparejar dentro del mismo grupo siempre que se pueda.
- Evitar repetir un cruce entre los mismos dos jugadores mientras existan alternativas.
- Con numero impar de jugadores, **bye** (victoria automatica) al jugador de menor puntuacion que
  aun no haya recibido uno.
- Al terminar la fase suiza: clasificacion por puntos y desempates y, si el torneo tiene top cut,
  bracket de eliminacion directa del tamano elegido (top 4, 8, 16...).

El top cut es **opcional** y el organizador puede activarlo, desactivarlo o cambiar su tamano en
cualquier momento **hasta que se lanza la primera ronda**.

### Rulesets por TCG

Las reglas de Magic y One Piece **no se hardcodean** en la logica de torneos. Existe un `Ruleset`
configurable por TCG con, como minimo:

- **Formato de partida por fase** (Bo1 / Bo3), configurable de forma independiente para la fase
  suiza y para el top cut. Por defecto Bo1 en ambas para One Piece y Bo3 en ambas para Magic,
  pero una tienda puede combinarlos (p. ej. Bo3 en suizo y Bo1 en top cut).
- **Sistema de puntos** (puntos por victoria / empate / derrota).
- **Criterios de desempate y su orden** (en Magic: % de victorias de los rivales, % de partidas
  ganadas, % de partidas ganadas por los rivales).
- **Duracion de ronda por defecto** (ajustable por la tienda al crear el torneo).

Los rulesets de Magic y One Piece son **datos de configuracion (seed)**, no codigo especifico por
juego: activar un tercer TCG sera dar de alta su ruleset, no escribir clases nuevas. Los puntos y
desempates exactos se validan contra las reglas oficiales vigentes al implementarlos.

Las **ligas por temporada** reutilizan el mismo modelo de puntuacion, acumulando puntos a lo largo
de varios eventos en vez de un unico torneo.

### Detalles del modelo de torneo

- **Participantes sin cuenta**: no todo participante tiene usuario. Existe un `Participante`
  propio de cada torneo con vinculo *opcional* a un `Usuario` registrado.
- **Configuracion fijada por torneo**: al crear un torneo se *copian* los valores del ruleset del
  TCG. Un cambio posterior en el ruleset no altera torneos ya creados.
- **Resultado por partida en Bo3**: se registra cada partida del enfrentamiento, no solo el
  resultado global (los desempates por % de partidas ganadas lo necesitan).
- **Bajas a mitad de torneo**: el jugador retirado queda excluido de emparejamientos futuros (sin
  byes ni rivales nuevos) pero conserva sus resultados ya jugados a efectos de clasificacion.
- **Fin del tiempo de ronda**: si el cronometro llega a cero sin resultado reportado, la partida
  se marca automaticamente como **empate**; el organizador puede corregirla a mano.

## Evolucion prevista

1. Web app para la tienda local de Jaime. **← v1**
2. App Android nativa, distribuida primero como `.apk` para conocidos.
3. Si crece: publicacion en Google Play y, si es viable, iOS.
4. Apertura a otras tiendas y otros TCGs (Pokemon, Yu-Gi-Oh, Digimon...), aprovechando que la
   arquitectura ya es multi-tenant desde la v1.
