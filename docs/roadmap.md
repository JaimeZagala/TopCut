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

### Hito 3 — UI de gestion de torneo (tienda) ✅

Partido en cuatro tramos para que cada uno sea revisable por separado.

#### 3a — Cascara, tema y textos ✅

- ✅ **MudBlazor 9** con un tema unico (`Theme/TopCutTheme.cs`), paleta clara y oscura, y la pila de
  fuentes del sistema: **ninguna fuente externa**, para no enviar la IP de cada visitante a un CDN
  en la misma aplicacion que muestra un aviso de proteccion de datos
- ✅ Fuera Bootstrap y lo que quedaba de la plantilla (contador, previsión del tiempo)
- ✅ **Dos cascaras**: `MainLayout` (publica) y `StoreLayout` (gestion, con cajon lateral)
- ✅ **Interactividad global con paginas excluidas** (ADR-0020): las publicas y las de cuenta se
  sirven en SSR estatico, de modo que leer la portada no gasta una de las ~20 conexiones del nivel
  gratuito de Azure SignalR
- ✅ **Textos de interfaz en `.resx`** con clase generada por MSBuild (ADR-0019): una clave mal
  escrita es un error de compilacion, no una cadena rara en pantalla
- ✅ Registro, acceso, alta con Google y aviso de privacidad **reestilados**, con los campos de
  formulario preparados para SSR estatico (ADR-0021)
- ✅ Paginas de error, no encontrado y sin permiso, ahora si con contenido: la reejecucion de
  `/not-found` devolvia un 404 vacio porque el enrutado se resolvia antes
- ✅ **Politicas solo por cookie** (ADR-0022) y destinos de redireccion propios; el destino de vuelta
  tras entrar se valida para que no apunte fuera del sitio
- ✅ Corregido un fallo real que aparecio al abrir la primera pantalla de gestion: la comprobacion de
  tienda aprobada compartia el `DbContext` de la peticion y reventaba al evaluarse dos veces a la
  vez (ADR-0023)
- ✅ **17 pruebas nuevas**: 4 que fijan como se renderizan los campos de MudBlazor sin base de datos
  y 13 que recorren la aplicacion real por HTTP (alta desde el formulario, consentimiento rechazado,
  redirecciones, permisos y que cada pagina abra o no circuito)

#### 3b — CRUD de torneos ✅

- ✅ **Listado** de los torneos de la tienda, con el proximo arriba: juego, fecha, estructura,
  inscritos y lista de espera, y estado
- ✅ **Crear** y **editar**, con el mismo formulario: datos, sede, estructura y las reglas de juego
  del torneo (formato y reloj de cada fase, y que pasa al acabarse el tiempo)
- ✅ La pantalla de crear **llega rellena**: elegir el juego trae sus reglas, y las rondas y el top
  cut salen de su tabla oficial segun las plazas (ADR-0018). Cambiar de juego o de plazas actualiza
  la sugerencia, y hay un boton para aplicarla
- ✅ **Publicar, volver a borrador y cancelar**, con confirmacion en el unico que no tiene vuelta
- ✅ Con el torneo ya en curso la configuracion se **bloquea**, porque cambiarla invalidaria lo jugado
- ✅ **Acceso a datos por operacion** (ADR-0026): proyecciones sin seguimiento para las listas y el
  agregado completo solo para cambiar, cada uno con su propia conexion
- ✅ **La tienda en contexto tambien dentro del circuito** (ADR-0024). Sin esto la tienda habria
  abierto su lista y la habria visto vacia, sin ningun error: no hay `HttpContext` dentro de un
  circuito de SignalR, y el filtro de tenant falla cerrado
- ✅ **Zona horaria configurada** (ADR-0025), para que la hora tecleada signifique lo mismo en el
  portatil y en un servidor que corre en UTC
- ✅ **18 pruebas nuevas**: 11 sobre crear, editar, publicar y el aislamiento entre tiendas contra
  SQL Server real, 4 sobre las pantallas por HTTP, y 3 sobre el ambito de tienda

#### 3c — Participantes ✅

- ✅ **Alta en el mostrador**: se escribe el nombre y se pulsa Intro, y el cursor se queda donde
  estaba. Apuntar a dieciseis personas no deberia exigir tocar el raton dieciseis veces
- ✅ **Sin cuenta**: un participante es un nombre. Quien si se registro desde la aplicacion lleva su
  distintivo, y dos personas pueden llamarse igual, porque a un jugador que entra por la puerta no
  hay mas cosa que lo distinga
- ✅ **Lista de espera** a la vista, con su orden: al llenarse las plazas los siguientes entran en
  cola, y el alta dice en que puesto quedan
- ✅ **Bajas** con confirmacion, y el aviso dice lo que significa dar de baja **en ese momento**:
  antes de empezar libera la plaza y entra la primera persona de la cola (y se dice quien), y con el
  torneo en marcha solo deja de emparejarse, conservando lo que ya jugo
- ✅ **Corregir un nombre** mal escrito
- ✅ Pestanas por torneo (*Datos* / *Participantes*), cada una con su direccion propia
- ✅ Un choque de concurrencia al guardar deja de ser un error de servidor y se explica
- ✅ **15 pruebas nuevas**, 13 sobre el roster contra SQL Server real y 2 sobre las pantallas

#### 3d — Mesa de control del torneo ✅

- ✅ **Una pantalla, un boton**: la mesa ensena en cada momento lo unico que toca hacer — lanzar la
  ronda, cerrarla, resolver por fin de tiempo o finalizar el torneo
- ✅ **Emparejamientos** con los nombres puestos, ordenados por mesa, con el bye ya resuelto
- ✅ **Resultados por marcador**: los botones son los que el reglamento del torneo admite (1-0 /
  empate / 0-1 en Bo1, y 2-0 / 2-1 / empate / 1-2 / 0-2 en Bo3), leidos siempre desde el jugador de
  la izquierda. Nadie teclea partida a partida
- ✅ **Corregir** cualquier resultado, tambien de una ronda ya cerrada, y queda marcado como corregido
- ✅ **Clasificacion en vivo** con una columna por desempate **del ruleset del torneo**: Magic ensena
  OMW%, GW% y OGW%; One Piece, su porcentaje propio, el de los rivales y el enfrentamiento directo
- ✅ **Fin de tiempo** en un paso, aplicando la politica del torneo a lo que siga sin reportar
- ✅ **Rondas anteriores** consultables, cada una con su direccion, para poder volver a una y corregir
- ✅ Todo sale de **una sola lectura** del torneo, para que emparejamientos y clasificacion no puedan
  contradecirse
- ✅ **16 pruebas nuevas**, entre ellas la que comprueba que **cada boton que ensena la pantalla es un
  resultado que las reglas aceptan** y que se guarda con el marcador que promete

*Criterio de cierre del hito 3 alcanzado*: una tienda crea un torneo, apunta a los jugadores, lanza
rondas, registra y corrige resultados y lo cierra, todo desde el navegador.

### Hito 4 — Tiempo real: pantalla en vivo y cronometro 🔨

- ✅ **Las pantallas se actualizan solas**: lanzar una ronda, meter un resultado, corregirlo, cerrar
  la ronda o dar una baja aparece en cualquier otra pantalla abierta de ese torneo sin recargar
- ✅ **Pantalla en vivo** para proyectar en la sala (`/tienda/torneos/{id}/en-vivo`): sin barras ni
  menus, tipografia que se lee de lejos, mesas mientras se juega y **clasificacion entre rondas**
- ✅ **Cronometro de ronda sincronizado** (ADR-0028): el servidor manda el instante de fin en UTC y
  su propia hora, y la cuenta atras la lleva el navegador. Recargar no lo reinicia, un equipo con la
  hora mal puesta lo ve bien, y no cuesta un mensaje por segundo y espectador
- ✅ **Primer suscriptor de los eventos de dominio** (ADR-0010), que hasta ahora se levantaban y se
  tiraban. Se anade `RoundClosed`, que faltaba
- ✅ **17 pruebas nuevas**, incluida la que recorre la cadena entera contra SQL Server real: guardar,
  interceptor, despachador, handler y aviso a la pantalla
- 🔁 **Sin hub de SignalR propio** (ADR-0027). Toda pantalla de TopCut es una pagina de Blazor
  Server y **ya tiene su WebSocket**; un hub aparte serian dos conexiones por espectador cuando el
  plan gratuito de App Service permite cinco en total. El hub sigue teniendo sentido para clientes
  **externos** (la app movil), y va con la Web API externa del hito 8
- ⏸ **Pantalla movil del jugador para reportar en un par de toques**: necesita que un jugador este
  vinculado a su participante, y ese vinculo lo crea la inscripcion, que es el hito 5. Pendiente de
  confirmar si se hace al principio del hito 5 o se adelanta atando la cuenta a mano

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
