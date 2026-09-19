# Modelo de datos

> Ultima actualizacion: 2026-09-19 (hito 1a)
>
> **Estado: modelo de dominio implementado, persistencia pendiente.** Las entidades viven en
> `src/TopCut.Domain`. Todavia no hay `DbContext` ni migraciones: el mapeo a SQL Server y los
> indices llegan en el hito 1b y este documento se ampliara entonces con el esquema real.

## Convenciones

- **Claves primarias mixtas** (ADR-0006): `int` autoincremental en catalogos (`Tcg`, `Ruleset`),
  `Guid` v7 secuencial (`Guid.CreateVersion7()`) en entidades de negocio expuestas en URLs
  (`Tienda`, `Torneo`, `Participante`, `Ronda`, `Emparejamiento`, `Partida`, `Liga`). Los IDs de
  Identity (`AspNetUsers`, `AspNetRoles`) se quedan como `string`, el default del framework.
- `Tenant` y `Tienda` son **la misma entidad**: una tienda *es* un tenant.
- **Fechas y horas en UTC** (`DateTimeOffset`), convertidas a hora local solo al presentarlas. Es
  lo que hace fiable el cronometro de ronda frente a reconexiones.
- **Nomenclatura en espanol** para el dominio (ADR-0008).

## Piezas comunes

| Tipo | Para que |
|---|---|
| `Entidad<TId>` | Raiz comun. Acumula eventos de dominio que la infraestructura publicara al guardar. |
| `EntidadDeNegocio` / `EntidadDeCatalogo` | Fijan el tipo de clave segun ADR-0006. |
| `IPerteneceATenant` | Marca las filas de una tienda. El filtro global de EF Core se apoya en ella. |
| `IAuditable` | `CreadoEn` / `ModificadoEn`, que rellenara un interceptor. |
| `IConControlDeConcurrencia` | `RowVersion` para concurrencia optimista. |
| `ExcepcionDeDominio` | Violacion de invariante. La presentacion la traduce a 400/409, nunca a un 500. |

## Catalogo de juegos

### `Tcg`

Codigo corto y estable (`MTG`, `OPTCG`), nombre y bandera de activo. Lo gestiona el Superadmin.

### `Ruleset`

Reglas de competicion **como datos**. Ninguna logica de torneo pregunta que juego es.

| Campo | Notas |
|---|---|
| `PuntosVictoria` / `PuntosEmpate` / `PuntosDerrota` / `PuntosBye` | Puntuacion del enfrentamiento. |
| `PuntosPartidaVictoria` / `...Empate` / `...Derrota` | Puntuacion de la partida individual; solo la usan los desempates por partidas. |
| `ByeCuentaEnPorcentajePropio` | **Aqui los dos juegos difieren** (ver abajo). |
| `PorcentajeMinimo` | Suelo aplicado a los porcentajes antes de promediarlos. |
| `FaseSuiza` / `FaseTopCut` | Sendos `ConfiguracionFase` independientes. |
| `CriteriosDesempate` | Lista **ordenada**; el orden es significativo. |
| `VersionReglas` / `FuenteOficial` | Trazabilidad: contra que documento se valido y donde esta. |

`ConfiguracionFase` es un value object con `Formato` (Bo1/Bo3), `DuracionRonda` (nula = sin limite)
y `TiempoExtra` (prorroga de reloj).

### Valores del seed, verificados contra los reglamentos oficiales

| | Magic: The Gathering | One Piece Card Game |
|---|---|---|
| Fuente | Magic Tournament Rules, 21 abr 2025 | Bandai OP Tournament Rules Manual, 17 oct 2025 |
| Puntos V/E/D | 3 / 1 / 0 | 3 / 1 / 0 |
| Bye | 3 puntos (victoria 2-0) | 3 puntos |
| Formato en suizo | Bo3 | **Bo1** |
| Formato en top cut | Bo3 | Bo3 |
| Ronda en suizo | 50 min, sin prorroga | 30 min + 5 de prorroga |
| Ronda en top cut | 90 min, sin prorroga | 60 min + 10 de prorroga |
| Desempates, en orden | % victorias rivales → % partidas ganadas → % partidas ganadas rivales | % victorias propio → % victorias rivales → enfrentamiento directo |
| Suelo de porcentaje | 0,33 | 0,33 |
| El bye entra en el % propio | **Si** | **No** |

Esa ultima fila es la razon concreta de que el ruleset sea configuracion y no codigo: Magic cuenta
el bye como una victoria mas a todos los efectos, y One Piece lo excluye del numerador y del
denominador. `RulesetsOficialesTests` blinda toda la tabla; si una organizacion revisa su
reglamento, esos tests son los primeros que deben fallar.

## Tenencia

### `Tienda` (tenant)

Nombre, `Direccion`, estado de aprobacion (`PendienteDeAprobacion` / `Aprobada` / `Revocada`) y
`EsDePruebas`. Solo una tienda aprobada puede organizar. El tenant de pruebas queda fuera de las
busquedas publicas y sustituye a un entorno de staging, que el nivel gratuito de Azure SQL no
permite costear.

### `Direccion` (value object)

Calle, ciudad, provincia y codigo postal **descompuestos**, mas `Latitud` / `Longitud` opcionales y
**sin uso en la v1**. Activar "torneos cerca de mi" sera anadir una consulta por distancia.

> No hay campo `Pais`: la v1 es solo Espana y el brief no lo pide. Anadirlo mas adelante es una
> migracion de una columna.

## Torneos

`Torneo` es la **raiz del agregado**: participantes, rondas y emparejamientos se modifican siempre
a traves de el.

```
Tienda 1──* Torneo *──1 Tcg 1──1 Ruleset
   │            │
   │            ├──* Participante ──0..1 Usuario (Identity)
   │            │
   │            └──* Ronda ──* Emparejamiento ──* Partida
   │
   └──* Liga ◄──── Torneo.LigaId
```

| Entidad | Puntos relevantes |
|---|---|
| `Torneo` | `TenantId`, TCG, nombre, fecha, `Lugar`, plazas, rondas suizas, `TamanoTopCut` (nulo = sin top cut), estado y `Configuracion`. `RowVersion`. |
| `ConfiguracionTorneo` | **Copia** del ruleset hecha al crear el torneo. Guarda `RulesetOrigenId` y `VersionReglas` solo como trazabilidad: no se resincroniza jamas. |
| `Participante` | `Nombre` siempre presente; `UsuarioId` **opcional** (altas manuales de mostrador). Estado `Inscrito` / `EnListaDeEspera` / `DadoDeBaja` y `PosicionEnListaDeEspera`. `RowVersion`. |
| `Ronda` | Numero, fase, formato copiado de la fase, y los instantes `FinTiempoRegularEn` y `FinConProrrogaEn` en UTC. |
| `Emparejamiento` | Mesa, dos participantes (o uno y un bye), estado, `ReportadoPor`, `CorregidoPorOrganizador`, `EmpatePorTiempoAgotado`. `RowVersion`. |
| `Partida` | Partida individual con su numero y ganador. En Bo1 hay una; en Bo3 dos o tres. |

### Invariantes ya implementadas

- El **top cut** se puede activar, cambiar de tamano o quitar **hasta que se lanza la primera
  ronda**; despues la configuracion queda congelada. Solo admite potencias de dos.
- Cada **fase** tiene su propio formato y su propio tiempo, configurables por separado.
- Al llenarse el torneo, las inscripciones pasan a **lista de espera** conservando el orden. Una
  baja antes de empezar promociona al primero y reordena al resto; una baja con el torneo en curso
  no promociona a nadie, pero retira al jugador de los emparejamientos futuros conservando sus
  resultados.
- Un **bye** nace ya resuelto y no admite que se le reporte resultado.
- Un reporte **no puede incluir partidas jugadas despues de que el enfrentamiento estuviera
  decidido** (un 2-1 con la tercera partida de mas es un reporte invalido).
- Una **ronda con resultados pendientes no se puede cerrar**, y un torneo con rondas abiertas no se
  puede finalizar.

## Eventos de dominio

`RondaLanzada`, `ResultadoRegistrado`, `ResultadoPendiente`, `PlazaLiberada`, `TorneoFinalizado`.
En la v1 **no tienen suscriptores**: existen para que anadir notificaciones o refrescar la pantalla
en vivo por SignalR sea escribir un handler, sin tocar el dominio.

## Ligas

`Liga` pertenece a una tienda y a un TCG, con temporada (`Comienza` / `Termina`) y bandera de
abierta. `Torneo.LigaId` apunta a ella, asi que un torneo puntua **como mucho para una liga**. La
clasificacion acumulada llega en el hito 6.

## Pendiente

- **Hito 1b**: mapeo EF Core, filtros globales por `TenantId`, indices, primera migracion.
- **Hito 2**: politica de fin de tiempo configurable (ADR-0009) y el calculo de clasificacion.
