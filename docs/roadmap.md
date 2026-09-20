# Roadmap

> Ultima actualizacion: 2026-09-20
>
> Leyenda: ✅ hecho · 🔨 en curso · ⏸ pendiente de confirmar · ⬜ no empezado

## Fase 1 — Web app para la tienda de Jaime (v1)

### Hito 0 — Esqueleto de la solucion ✅

- ✅ Solucion .NET 10 con las capas Domain / Application / Infrastructure / Api / Web
- ✅ Tres proyectos de pruebas (Domain, Application, Integracion)
- ✅ Referencias entre capas segun las reglas de dependencia
- ✅ `Directory.Build.props` + `Directory.Packages.props` (versiones centralizadas)
- ✅ `.gitignore`, `.gitattributes`, `.editorconfig`, `global.json`, `README.md`
- ✅ Carpeta `/docs` con los seis documentos vivos
- ✅ `dotnet build` y `dotnet test` en verde
- ✅ Decisiones confirmadas: topologia de `TopCut.Api` (ADR-0004), herramientas (ADR-0005),
  claves primarias (ADR-0006)
- ✅ Herramientas: git 2.55, Docker Desktop 29.8 (contenedores Linux) y GitHub CLI 2.101
- ✅ Base de datos de desarrollo en Docker via `compose.yaml` (ADR-0015)
- ✅ Repositorio local inicializado
- ✅ Repositorio remoto: `JaimeZagala/TopCut` (privado), `main` sincronizada (ADR-0007)
- ⏸ Correo dedicado del proyecto para Azure, Google Cloud y Play Console (ADR-0007)

### Hito 1 — Modelo de datos, multi-tenancy e identidad 🔨

Partido en tres tramos para que cada uno sea revisable por separado.

#### 1a — Modelo de dominio y rulesets ✅

- ✅ Entidades: `Store`, `Address`, `Tcg`, `Ruleset`, `Tournament`, `TournamentSettings`,
  `Participant`, `Round`, `Pairing`, `Game`, `League`
- ✅ Piezas comunes: claves mixtas (ADR-0006), `ITenantOwned`, `IAuditable`, `RowVersion`
- ✅ Eventos de dominio sin suscriptores (`RoundStarted`, `ResultRecorded`, `RoundTimeExpired`,
  `SeatFreed`, `TournamentCompleted`)
- ✅ Rulesets de Magic y One Piece **verificados contra los reglamentos oficiales vigentes**
- ✅ Politica de fin de tiempo configurable y reporte partida a partida (ADR-0009)
- ✅ Codigo renombrado a ingles (ADR-0008)
- ✅ 64 pruebas de dominio en verde

#### 1b — Persistencia ✅

- ✅ `TopCutDbContext` con las nueve entidades mapeadas y sus indices
- ✅ Filtros globales por `TenantId` aplicados por convencion y **fallando cerrado** (ADR-0011)
- ✅ `RowVersion` e interceptor de auditoria con `TimeProvider`
- ✅ Despachador de eventos de dominio propio, que publica tras confirmar el guardado (ADR-0010)
- ✅ Seed idempotente del catalogo
- ✅ Migracion `InitialSchema` y su script revisable en `docs/migraciones/001-InitialSchema.sql`
- ✅ 20 pruebas de integracion, incluida la que confirma que una tienda no ve los datos de otra
- ✅ Verificadas **contra SQL Server real en Docker via Testcontainers**, que es el camino por
  defecto; tambien pasan contra LocalDB con la valvula del ADR-0012

#### 1c — Identidad ✅

- ✅ ASP.NET Core Identity sobre el mismo `DbContext`, con `TopCutUser` (nombre visible,
  consentimiento con su version, y la tienda a la que pertenece)
- ✅ Los tres roles y sus politicas; `CanOrganize` comprueba la aprobacion **contra la base de
  datos**, no contra un claim, para que revocar una tienda surta efecto en la peticion siguiente
- ✅ Consentimiento RGPD exigido **en el servidor**, con [aviso](../src/TopCut.Web/Components/Account/PrivacyNotice.razor)
  versionado en `/privacidad`
- ✅ Paginas de registro, acceso y salida (sin estilar: MudBlazor llega en el hito 3)
- ✅ Login con Google cableado y **condicionado a que existan credenciales**, con su propia pantalla
  de consentimiento antes de crear la cuenta
- ✅ Tokens Bearer para clientes externos; los endpoints `/api/v1/*` **solo** aceptan Bearer (ADR-0013)
- ✅ Semilla de roles y del Superadmin de arranque desde configuracion
- ✅ Migracion `IdentitySchema` con su script en `docs/migraciones/002-IdentitySchema.sql`
- ✅ 11 pruebas de extremo a extremo que arrancan la aplicacion real y hablan HTTP de verdad

*Criterio de cierre alcanzado*: un jugador se registra desde el navegador y el Superadmin aprueba
una tienda.

### Hito 2 — Motor de torneo ✅

- ✅ **Emparejador suizo Dutch** por backtracking (ADR-0016): agrupacion por puntuacion, mitad
  superior contra mitad inferior, y repeticion de cruce solo cuando no queda alternativa
- ✅ **Byes** al jugador peor clasificado que aun no tenga uno; nadie repite mientras alguien este sin
- ✅ **Clasificacion** con los desempates del ruleset aplicados en su orden, con su suelo, y con el
  bye contando o no segun el TCG
- ✅ **Enfrentamiento directo** como ultimo recurso entre dos jugadores aislados (ADR-0017)
- ✅ **Top cut** opcional: sembrado 1-contra-ultimo desde la clasificacion final, y el bracket se
  estrecha emparejando ganadores; el empate en eliminatoria se rechaza en vez de inventarse
- ✅ **Bajas** a mitad de torneo: fuera de los emparejamientos, dentro de la clasificacion
- ✅ `TournamentEngine`, punto de entrada unico que decide que fase toca y lanza la ronda
- ✅ `TournamentService` en Application, con las invariantes rotas devueltas como mensaje
- ✅ **145 pruebas de dominio**, incluidas simulaciones completas de campos de 4 a 128 jugadores,
  con numeros impares y no potencias de dos (7, 19, 27, 37, 53, 101), y el caso limite del round
  robin completo
- ✅ **6 pruebas de integracion** que recorren torneos enteros recargando el agregado desde SQL
  Server entre cada operacion, como haria la aplicacion entre peticiones

- ✅ **Tabla de rondas y cut por asistencia** en el ruleset, con los valores oficiales de cada juego
  (ADR-0018), para que la pantalla de crear torneo del hito 3 llegue con el numero correcto puesto

*Criterio de cierre alcanzado*: torneos completos simulados con campos pares e impares, con bajas,
con y sin top cut, verificando emparejamientos y clasificacion.

### Hito 3 — UI de gestion de torneo (tienda) ⬜

MudBlazor, layout base, textos en `.resx`. CRUD de torneos, alta manual de participantes, lanzar
rondas, ver y corregir resultados. Todavia sin tiempo real.

### Hito 4 — Tiempo real: pantalla en vivo y cronometro ⬜

Hub de SignalR, pantalla en vivo del organizador, **cronometro de ronda sincronizado** (el servidor
manda el instante de fin en UTC), y la pantalla movil del jugador para **reportar resultado en un
par de toques**.

### Hito 5 — Cara publica: busqueda, inscripcion y lista de espera ⬜

Busqueda por ciudad / tienda / TCG / fecha en SSR estatico, inscripcion y baja, **lista de espera
con promocion automatica** (con concurrencia optimista al ocupar la ultima plaza), historial del
jugador.

### Hito 6 — Ligas por temporada ⬜

Acumulacion de puntos entre torneos reutilizando el modelo de puntuacion, clasificacion de liga.

### Hito 7 — Panel de Superadmin ⬜

Aprobar y revocar el rol de tienda, catalogo de TCGs, tiempos de ronda por defecto de la plataforma.

### Hito 8 — Web API externa, despliegue y CI/CD ⬜

Endpoints REST con Bearer y [`api.md`](api.md) al dia. Despliegue en App Service F1 + Azure SQL
gratuito + Azure SignalR gratuito, y GitHub Actions con OpenID Connect.

## Fase 2 — App Android nativa (.apk) ⬜

Blazor Hybrid sobre .NET MAUI reutilizando los componentes MudBlazor, consumiendo la Web API con
tokens Bearer. Aqui entran las **notificaciones push** (Firebase Cloud Messaging), anadiendo
handlers a los eventos de dominio ya existentes.

## Fase 3 — Publicacion en tiendas ⬜

Google Play y, si es viable, iOS.

## Fase 4 — Apertura a mas tiendas y mas TCGs ⬜

Pokemon, Yu-Gi-Oh, Digimon... Sin cambios de arquitectura: dar de alta el ruleset desde el panel de
Superadmin. Aqui tambien entra la **geolocalizacion real** ("torneos cerca de mi"), aprovechando
los campos `Latitud`/`Longitud` reservados desde la v1.
