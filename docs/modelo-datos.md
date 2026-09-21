# Modelo de datos

> Última actualización: 2026-09-20 (hito 1b)
>
> **Estado: modelo de dominio y persistencia implementados.** Las entidades viven en
> `src/TopCut.Domain` y su mapeo en `src/TopCut.Infrastructure/Persistence`. El esquema real está
> en la migración `InitialSchema`, con su script revisable en
> [`migraciones/001-InitialSchema.sql`](migraciones/001-InitialSchema.sql).

## Convenciones

- **Claves primarias mixtas** (ADR-0006): `int` autoincremental en catálogos (`Tcg`, `Ruleset`),
  `Guid` v7 secuencial (`Guid.CreateVersion7()`) en entidades de negocio expuestas en URLs
  (`Store`, `Tournament`, `Participant`, `Round`, `Pairing`, `Game`, `League`). Los IDs de
  Identity (`AspNetUsers`, `AspNetRoles`) se quedan como `string`, el default del framework.
- `Tenant` y `Store` son **la misma entidad**: una tienda *es* un tenant.
- **Fechas y horas en UTC** (`DateTimeOffset`), convertidas a hora local solo al presentarlas. Es
  lo que hace fiable el cronómetro de ronda frente a reconexiones.
- **Código en inglés**; `/docs` y los mensajes de error, en español (ADR-0008).

## Piezas comunes

| Tipo | Para que |
|---|---|
| `Entity<TId>` | Raíz común. Acumula eventos de dominio que la infraestructura publicará al guardar. |
| `BusinessEntity` / `CatalogEntity` | Fijan el tipo de clave segun ADR-0006. |
| `ITenantOwned` | Marca las filas de una tienda. El filtro global de EF Core se apoya en ella. |
| `IAuditable` | `CreatedAt` / `UpdatedAt`, que rellenara un interceptor. |
| `IHasRowVersion` | `RowVersion` para concurrencia optimista. |
| `DomainException` | Violación de invariante. La presentación la traduce a 400/409, nunca a un 500. |

## Catálogo de juegos

### `Tcg`

Código corto y estable (`MTG`, `OPTCG`), nombre y bandera de activo. Lo gestiona el Superadmin.

### `Ruleset`

Reglas de competición **como datos**. Ninguna lógica de torneo pregunta que juego es.

| Campo | Notas |
|---|---|
| `WinPoints` / `DrawPoints` / `LossPoints` / `ByePoints` | Puntuación del enfrentamiento. |
| `GameWinPoints` / `GameDrawPoints` / `GameLossPoints` | Puntuación de la partida individual; solo la usan los desempates por partidas. |
| `ByesCountInOwnWinPercentage` | **Aquí los dos juegos difieren** (ver abajo). |
| `MinimumPercentage` | Suelo aplicado a los porcentajes antes de promediarlos. |
| `SwissPhase` / `TopCutPhase` | Sendos `PhaseSettings` independientes. |
| `Tiebreakers` | Lista **ordenada**; el orden es significativo. |
| `AttendanceBands` | Rondas suizas y tamaño de cut recomendados segun la asistencia. Cada juego tiene la suya (ADR-0018). |
| `RulesVersion` / `OfficialSource` | Trazabilidad: contra qué documento se validó y dónde está. |

`PhaseSettings` es un value object con `Format` (Bo1/Bo3), `RoundLength` (nula = sin límite) y
`ExtraTime` (prórroga de reloj). `TimeExpiry` fija que pasa con lo no reportado al agotarse el
reloj (ADR-0009).

### Valores del seed, verificados contra los reglamentos oficiales

| | Magic: The Gathering | One Piece Card Game |
|---|---|---|
| Fuente | Magic Tournament Rules, 21 abr 2025 | Bandai OP Tournament Rules Manual, 17 oct 2025 |
| Puntos V/E/D | 3 / 1 / 0 | 3 / 1 / 0 |
| Bye | 3 puntos (victoria 2-0) | 3 puntos |
| Formato en suizo | Bo3 | **Bo1** |
| Formato en top cut | Bo3 | Bo3 |
| Ronda en suizo | 50 min, sin prórroga | 30 min + 5 de prórroga |
| Ronda en top cut | 90 min, sin prórroga | 60 min + 10 de prórroga |
| Desempates, en orden | % victorias rivales → % partidas ganadas → % partidas ganadas rivales | % victorias propio → % victorias rivales → enfrentamiento directo |
| Suelo de porcentaje | 0,33 | 0,33 |
| El bye entra en el % propio | **Si** | **No** |
| Recomendación para 12 jugadores | 5 rondas, Top 4 | 4 rondas, Top 2 |
| Fin de tiempo (ADR-0009) | Gana quien va por delante | Gana quien va por delante |

Esa última fila es la razón concreta de que el ruleset sea configuración y no código: Magic cuenta
el bye como una victoria más a todos los efectos, y One Piece lo excluye del numerador y del
denominador. `SeedRulesetsTests` blinda toda la tabla; si una organización revisa su
reglamento, esos tests son los primeros que deben fallar.

## Tenencia

### `Store` (tenant)

Nombre, `Address`, estado de aprobación (`PendingApproval` / `Approved` / `Revoked`) y
`IsTestStore`. Solo una tienda aprobada puede organizar. El tenant de pruebas queda fuera de las
búsquedas públicas y sustituye a un entorno de staging, que el nivel gratuito de Azure SQL no
permite costear.

### `Address` (value object)

Calle, ciudad, provincia y código postal **descompuestos**, más `Latitude` / `Longitude` opcionales y
**sin uso en la v1**. Activar "torneos cerca de mi" será añadir una consulta por distancia.

> No hay campo `Country`: la v1 es solo España y el brief no lo pide. Añadirlo más adelante es una
> migración de una columna.

## Torneos

`Tournament` es la **raíz del agregado**: participantes, rondas y emparejamientos se modifican siempre
a través de el.

```
Store 1──* Tournament *──1 Tcg 1──1 Ruleset
  │            │
  │            ├──* Participant ──0..1 User (Identity)
  │            │
  │            └──* Round ──* Pairing ──* Game
  │
  └──* League ◄──── Tournament.LeagueId
```

| Entidad | Puntos relevantes |
|---|---|
| `Tournament` | `TenantId`, TCG, nombre, fecha, `Venue`, `Capacity`, `SwissRounds`, `TopCutSize` (nulo = sin top cut), estado y `Settings`. `RowVersion`. |
| `TournamentSettings` | **Copia** del ruleset hecha al crear el torneo. Guarda `SourceRulesetId` y `RulesVersion` solo como trazabilidad: no se resincroniza jamás. |
| `Participant` | `Name` siempre presente; `UserId` **opcional** (altas manuales de mostrador). Estado `Registered` / `Waitlisted` / `Dropped` y `WaitlistPosition`. `RowVersion`. |
| `Round` | Número, fase, formato copiado de la fase, y los instantes `RegularTimeEndsAt` y `ExtraTimeEndsAt` en UTC. |
| `Pairing` | Mesa, dos jugadores (o uno y un bye), estado, `ReportedBy`, `AmendedByOrganizer`, `DecidedByTimeExpiry`. `RowVersion`. |
| `Game` | Partida individual con su número y ganador. En Bo1 hay una; en Bo3 dos o tres. |

### Invariantes ya implementadas

- El **top cut** se puede activar, cambiar de tamaño o quitar **hasta que se lanza la primera
  ronda**; después la configuración queda congelada. Solo admite potencias de dos.
- Cada **fase** tiene su propio formato y su propio tiempo, configurables por separado.
- Al llenarse el torneo, las inscripciones pasan a **lista de espera** conservando el orden. Una
  baja antes de empezar promociona al primero y **renumera la cola entera**, salga quien salga y
  desde donde salga; una baja con el torneo en curso no promociona a nadie, pero retira al jugador
  de los emparejamientos futuros conservando sus resultados.
- Una **cuenta** ocupa como mucho **una fila** en el torneo. Quien se da de baja y vuelve a
  apuntarse reincorpora su misma fila, no crea otra; y no recupera la plaza, que para entonces
  puede tenerla quien estuviera esperando.
- Un **bye** nace ya resuelto y no admite que se le reporte resultado.
- Las partidas se registran **una a una segun terminan** (`RecordGame`), y el enfrentamiento se
  cierra solo en cuanto queda decidido.
- Un reporte completo **no puede incluir partidas jugadas después de que el enfrentamiento
  estuviera decidido** (un 2-1 con la tercera partida de más es un reporte inválido).
- Al **agotarse el reloj**, lo pendiente se resuelve segun `TimeExpiry` (ADR-0009): por defecto gana
  quien va por delante en partidas, y queda marcado con `DecidedByTimeExpiry` para que el
  organizador pueda revisarlo.
- Una **ronda con resultados pendientes no se puede cerrar**, y un torneo con rondas abiertas no se
  puede finalizar.

## Eventos de dominio

`RoundStarted`, `ResultRecorded`, `RoundTimeExpired`, `SeatFreed`, `TournamentCompleted`.
En la v1 **no tienen suscriptores**: existen para que añadir notificaciones o refrescar la pantalla
en vivo por SignalR sea escribir un handler, sin tocar el dominio.

## Ligas

`League` pertenece a una tienda y a un TCG, con temporada (`StartsOn` / `EndsOn`) y bandera de
abierta. `Tournament.LeagueId` apunta a ella, así que un torneo puntua **como mucho para una liga**. La
clasificación acumulada llega en el hito 6.

## Esquema en SQL Server

Nueve tablas más `__EFMigrationsHistory`. Los tipos propiedad se aplanan en la fila de su dueño en
lugar de crear tablas aparte, porque nunca se consultan por separado y un join no aportaría nada:

| Concepto | Como queda en la base de datos |
|---|---|
| `Address` | Columnas `Venue_Street`, `Venue_City`... dentro de `Tournaments`, y `Address_*` dentro de `Stores`. |
| `PhaseSettings` | `SwissPhase_Format`, `SwissPhase_RoundLength`, `SwissPhase_ExtraTime` y sus equivalentes de top cut. |
| `TournamentSettings` | Prefijo `Settings_` en `Tournaments`, con las fases anidadas dentro (`Settings_SwissPhase_Format`...). |
| `Tiebreakers` | Una sola columna `nvarchar(64)` con los criterios separados por comas: la lista es corta, el orden se conserva solo y nadie consulta un criterio suelto. Lleva un `ValueComparer` para que EF detecte una reordenación. |

### Índices

- `Tcgs.Code` único: aparece en URLs y filtros.
- `Rulesets.TcgId` único: un ruleset por juego.
- `Tournaments` por `(Status, StartsAt)` para la búsqueda pública, por `(TenantId, Status)` para el
  panel de la tienda, y por `TcgId` y `LeagueId`.
- `Participants` único y **filtrado** por `(TournamentId, UserId) WHERE UserId IS NOT NULL`: una
  cuenta no ocupa dos plazas del mismo torneo, pero los jugadores de mostrador, que no tienen
  cuenta, comparten el `NULL` sin chocar.
- `Rounds` único por `(TournamentId, Number)`; `Pairings` único por `(RoundId, Table)`;
  `Games` único por `(PairingId, Number)`.
- `Stores` por `(Status, IsTestStore)`, que es como filtra la búsqueda pública.

### Decisiones de integridad

- **`Pairing` no tiene clave ajena a `Participant`** a propósito: borrar un participante nunca debe
  arrastrar en cascada un enfrentamiento que ya cuenta para la clasificación.
- **`RowVersion`** en `Tournaments`, `Participants` y `Pairings`, que son las filas donde dos
  acciones simultáneas pueden pisarse. La del torneo protege **todo el agregado**, no solo su fila:
  cualquier escritura dentro del agregado marca también la raíz, para que dos inscripciones a la
  vez por la última plaza choquen en vez de colarse las dos (ADR-0032).
- **Fechas** como `datetimeoffset`; las duraciones de ronda como `time`.
- Las claves `Guid` v7 las genera el dominio, no la base de datos (`ValueGeneratedNever`).

## Seed del catálogo

`CatalogSeeder` crea los dos TCG y sus rulesets pasando por los mismos constructores del dominio que
usa la aplicación, en lugar de `HasData`, que lleva mal los tipos propiedad. Es idempotente, así que
puede ejecutarse en cada arranque.

## El motor (hito 2)

No añade tablas: son funciones puras sobre el agregado ya cargado, en `src/TopCut.Domain`.

| Pieza | Que hace |
|---|---|
| `StandingsCalculator` | Puntos y los cuatro porcentajes de desempate, aplicando el orden, el suelo y el trato del bye que declare el ruleset. |
| `SwissPairer` | Emparejamiento Dutch por backtracking (ADR-0016) y asignación del bye. |
| `TopCutBracket` | Sembrado del cuadro desde la clasificación final y estrechamiento por rondas. |
| `TournamentEngine` | Decide que fase toca y lanza la ronda. Es el único punto de entrada que necesita la aplicación. |

`Standing` es un registro de solo lectura, no una entidad: se recalcula a partir de los resultados
cada vez que se pide. Guardarlo sería una copia que puede quedar desincronizada de los
enfrentamientos, que son la verdad.

## Pendiente

- **Hito 6**: acumulación de puntos de liga entre torneos, que si necesitara consultas propias.
