# Roadmap

> Última actualización: 2026-09-20
>
> Leyenda: ✅ hecho · 🔨 en curso · ⏸ pendiente de confirmar · ⬜ no empezado

## Fase 1 — Web app para la tienda de Jaime (v1)

### Hito 0 — Esqueleto de la solución ✅

- ✅ Solución .NET 10 con las capas Domain / Application / Infrastructure / Api / Web
- ✅ Tres proyectos de pruebas (Domain, Application, Integración)
- ✅ Referencias entre capas segun las reglas de dependencia
- ✅ `Directory.Build.props` + `Directory.Packages.props` (versiones centralizadas)
- ✅ `.gitignore`, `.gitattributes`, `.editorconfig`, `global.json`, `README.md`
- ✅ Carpeta `/docs` con los seis documentos vivos
- ✅ `dotnet build` y `dotnet test` en verde
- ✅ Decisiones confirmadas: topología de `TopCut.Api` (ADR-0004), herramientas (ADR-0005),
  claves primarias (ADR-0006)
- ✅ Herramientas: git 2.55, Docker Desktop 29.8 (contenedores Linux) y GitHub CLI 2.101
- ✅ Base de datos de desarrollo en Docker vía `compose.yaml` (ADR-0015)
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
- ✅ Política de fin de tiempo configurable y reporte partida a partida (ADR-0009)
- ✅ Código renombrado a inglés (ADR-0008)
- ✅ 64 pruebas de dominio en verde

#### 1b — Persistencia ✅

- ✅ `TopCutDbContext` con las nueve entidades mapeadas y sus índices
- ✅ Filtros globales por `TenantId` aplicados por convención y **fallando cerrado** (ADR-0011)
- ✅ `RowVersion` e interceptor de auditoría con `TimeProvider`
- ✅ Despachador de eventos de dominio propio, que pública tras confirmar el guardado (ADR-0010)
- ✅ Seed idempotente del catálogo
- ✅ Migración `InitialSchema` y su script revisable en `docs/migraciones/001-InitialSchema.sql`
- ✅ 20 pruebas de integración, incluida la que confirma que una tienda no ve los datos de otra
- ✅ Verificadas **contra SQL Server real en Docker vía Testcontainers**, que es el camino por
  defecto; también pasan contra LocalDB con la válvula del ADR-0012

#### 1c — Identidad ✅

- ✅ ASP.NET Core Identity sobre el mismo `DbContext`, con `TopCutUser` (nombre visible,
  consentimiento con su versión, y la tienda a la que pertenece)
- ✅ Los tres roles y sus políticas; `CanOrganize` comprueba la aprobación **contra la base de
  datos**, no contra un claim, para que revocar una tienda surta efecto en la petición siguiente
- ✅ Consentimiento RGPD exigido **en el servidor**, con [aviso](../src/TopCut.Web/Components/Account/PrivacyNotice.razor)
  versionado en `/privacidad`
- ✅ Páginas de registro, acceso y salida (sin estilar: MudBlazor llega en el hito 3)
- ✅ Login con Google cableado y **condicionado a que existan credenciales**, con su propia pantalla
  de consentimiento antes de crear la cuenta
- ✅ Tokens Bearer para clientes externos; los endpoints `/api/v1/*` **solo** aceptan Bearer (ADR-0013)
- ✅ Semilla de roles y del Superadmin de arranque desde configuración
- ✅ Migración `IdentitySchema` con su script en `docs/migraciones/002-IdentitySchema.sql`
- ✅ 11 pruebas de extremo a extremo que arrancan la aplicación real y hablan HTTP de verdad

*Criterio de cierre alcanzado*: un jugador se registra desde el navegador y el Superadmin aprueba
una tienda.

### Hito 2 — Motor de torneo ✅

- ✅ **Emparejador suizo Dutch** por backtracking (ADR-0016): agrupación por puntuación, mitad
  superior contra mitad inferior, y repetición de cruce solo cuando no queda alternativa
- ✅ **Byes** al jugador peor clasificado que aún no tenga uno; nadie repite mientras alguien este sin
- ✅ **Clasificación** con los desempates del ruleset aplicados en su orden, con su suelo, y con el
  bye contando o no segun el TCG
- ✅ **Enfrentamiento directo** como último recurso entre dos jugadores aislados (ADR-0017)
- ✅ **Top cut** opcional: sembrado 1-contra-último desde la clasificación final, y el bracket se
  estrecha emparejando ganadores; el empate en eliminatoria se rechaza en vez de inventarse
- ✅ **Bajas** a mitad de torneo: fuera de los emparejamientos, dentro de la clasificación
- ✅ `TournamentEngine`, punto de entrada único que decide que fase toca y lanza la ronda
- ✅ `TournamentService` en Application, con las invariantes rotas devueltas como mensaje
- ✅ **145 pruebas de dominio**, incluidas simulaciones completas de campos de 4 a 128 jugadores,
  con números impares y no potencias de dos (7, 19, 27, 37, 53, 101), y el caso límite del round
  robin completo
- ✅ **6 pruebas de integración** que recorren torneos enteros recargando el agregado desde SQL
  Server entre cada operación, como haría la aplicación entre peticiones

- ✅ **Tabla de rondas y cut por asistencia** en el ruleset, con los valores oficiales de cada juego
  (ADR-0018), para que la pantalla de crear torneo del hito 3 llegue con el número correcto puesto

*Criterio de cierre alcanzado*: torneos completos simulados con campos pares e impares, con bajas,
con y sin top cut, verificando emparejamientos y clasificación.

### Hito 3 — UI de gestión de torneo (tienda) ✅

Partido en cuatro tramos para que cada uno sea revisable por separado.

#### 3a — Cáscara, tema y textos ✅

- ✅ **MudBlazor 9** con un tema único (`Theme/TopCutTheme.cs`), paleta clara y oscura, y la pila de
  fuentes del sistema: **ninguna fuente externa**, para no enviar la IP de cada visitante a un CDN
  en la misma aplicación que muestra un aviso de protección de datos
- ✅ Fuera Bootstrap y lo que quedaba de la plantilla (contador, previsión del tiempo)
- ✅ **Dos cáscaras**: `MainLayout` (pública) y `StoreLayout` (gestión, con cajón lateral)
- ✅ **Interactividad global con páginas excluidas** (ADR-0020): las públicas y las de cuenta se
  sirven en SSR estático, de modo que leer la portada no gasta una de las ~20 conexiones del nivel
  gratuito de Azure SignalR
- ✅ **Textos de interfaz en `.resx`** con clase generada por MSBuild (ADR-0019): una clave mal
  escrita es un error de compilación, no una cadena rara en pantalla
- ✅ Registro, acceso, alta con Google y aviso de privacidad **reestilados**, con los campos de
  formulario preparados para SSR estático (ADR-0021)
- ✅ Páginas de error, no encontrado y sin permiso, ahora si con contenido: la reejecución de
  `/not-found` devolvía un 404 vacío porque el enrutado se resolvía antes
- ✅ **Políticas solo por cookie** (ADR-0022) y destinos de redirección propios; el destino de vuelta
  tras entrar se valida para que no apunte fuera del sitio
- ✅ Corregido un fallo real que apareció al abrir la primera pantalla de gestión: la comprobación de
  tienda aprobada compartía el `DbContext` de la petición y reventaba al evaluarse dos veces a la
  vez (ADR-0023)
- ✅ **17 pruebas nuevas**: 4 que fijan como se renderizan los campos de MudBlazor sin base de datos
  y 13 que recorren la aplicación real por HTTP (alta desde el formulario, consentimiento rechazado,
  redirecciones, permisos y que cada página abra o no circuito)

#### 3b — CRUD de torneos ✅

- ✅ **Listado** de los torneos de la tienda, con el próximo arriba: juego, fecha, estructura,
  inscritos y lista de espera, y estado
- ✅ **Crear** y **editar**, con el mismo formulario: datos, sede, estructura y las reglas de juego
  del torneo (formato y reloj de cada fase, y que pasa al acabarse el tiempo)
- ✅ La pantalla de crear **llega rellena**: elegir el juego trae sus reglas, y las rondas y el top
  cut salen de su tabla oficial segun las plazas (ADR-0018). Cambiar de juego o de plazas actualiza
  la sugerencia, y hay un botón para aplicarla
- ✅ **Publicar, volver a borrador y cancelar**, con confirmación en el único que no tiene vuelta
- ✅ Con el torneo ya en curso la configuración se **bloquea**, porque cambiarla invalidaría lo jugado
- ✅ **Acceso a datos por operación** (ADR-0026): proyecciones sin seguimiento para las listas y el
  agregado completo solo para cambiar, cada uno con su propia conexión
- ✅ **La tienda en contexto también dentro del circuito** (ADR-0024). Sin esto la tienda habría
  abierto su lista y la habría visto vacía, sin ningún error: no hay `HttpContext` dentro de un
  circuito de SignalR, y el filtro de tenant falla cerrado
- ✅ **Zona horaria configurada** (ADR-0025), para que la hora tecleada signifique lo mismo en el
  portátil y en un servidor que corre en UTC
- ✅ **18 pruebas nuevas**: 11 sobre crear, editar, publicar y el aislamiento entre tiendas contra
  SQL Server real, 4 sobre las pantallas por HTTP, y 3 sobre el ámbito de tienda

#### 3c — Participantes ✅

- ✅ **Alta en el mostrador**: se escribe el nombre y se pulsa Intro, y el cursor se queda donde
  estaba. Apuntar a dieciséis personas no debería exigir tocar el ratón dieciséis veces
- ✅ **Sin cuenta**: un participante es un nombre. Quien si se registro desde la aplicación lleva su
  distintivo, y dos personas pueden llamarse igual, porque a un jugador que entra por la puerta no
  hay más cosa que lo distinga
- ✅ **Lista de espera** a la vista, con su orden: al llenarse las plazas los siguientes entran en
  cola, y el alta dice en que puesto quedan
- ✅ **Bajas** con confirmación, y el aviso dice lo que significa dar de baja **en ese momento**:
  antes de empezar libera la plaza y entra la primera persona de la cola (y se dice quien), y con el
  torneo en marcha solo deja de emparejarse, conservando lo que ya jugó
- ✅ **Corregir un nombre** mal escrito
- ✅ Pestañas por torneo (*Datos* / *Participantes*), cada una con su dirección propia
- ✅ Un choque de concurrencia al guardar deja de ser un error de servidor y se explica
- ✅ **15 pruebas nuevas**, 13 sobre el roster contra SQL Server real y 2 sobre las pantallas

#### 3d — Mesa de control del torneo ✅

- ✅ **Una pantalla, un botón**: la mesa enseña en cada momento lo único que toca hacer — lanzar la
  ronda, cerrarla, resolver por fin de tiempo o finalizar el torneo
- ✅ **Emparejamientos** con los nombres puestos, ordenados por mesa, con el bye ya resuelto
- ✅ **Resultados por marcador**: los botones son los que el reglamento del torneo admite (1-0 /
  empate / 0-1 en Bo1, y 2-0 / 2-1 / empate / 1-2 / 0-2 en Bo3), leídos siempre desde el jugador de
  la izquierda. Nadie teclea partida a partida
- ✅ **Corregir** cualquier resultado, también de una ronda ya cerrada, y queda marcado como corregido
- ✅ **Clasificación en vivo** con una columna por desempate **del ruleset del torneo**: Magic enseña
  OMW%, GW% y OGW%; One Piece, su porcentaje propio, el de los rivales y el enfrentamiento directo
- ✅ **Fin de tiempo** en un paso, aplicando la política del torneo a lo que siga sin reportar
- ✅ **Rondas anteriores** consultables, cada una con su dirección, para poder volver a una y corregir
- ✅ Todo sale de **una sola lectura** del torneo, para que emparejamientos y clasificación no puedan
  contradecirse
- ✅ **16 pruebas nuevas**, entre ellas la que comprueba que **cada botón que enseña la pantalla es un
  resultado que las reglas aceptan** y que se guarda con el marcador que promete

*Criterio de cierre del hito 3 alcanzado*: una tienda crea un torneo, apunta a los jugadores, lanza
rondas, registra y corrige resultados y lo cierra, todo desde el navegador.

### Hito 4 — Tiempo real: pantalla en vivo y cronómetro 🔨

- ✅ **Las pantallas se actualizan solas**: lanzar una ronda, meter un resultado, corregirlo, cerrar
  la ronda o dar una baja aparece en cualquier otra pantalla abierta de ese torneo sin recargar
- ✅ **Pantalla en vivo** para proyectar en la sala (`/tienda/torneos/{id}/en-vivo`): sin barras ni
  menús, tipografía que se lee de lejos, mesas mientras se juega y **clasificación entre rondas**
- ✅ **Cronómetro de ronda sincronizado** (ADR-0028): el servidor manda el instante de fin en UTC y
  su propia hora, y la cuenta atrás la lleva el navegador. Recargar no lo reinicia, un equipo con la
  hora mal puesta lo ve bien, y no cuesta un mensaje por segundo y espectador
- ✅ **Primer suscriptor de los eventos de dominio** (ADR-0010), que hasta ahora se levantaban y se
  tiraban. Se añade `RoundClosed`, que faltaba
- ✅ **17 pruebas nuevas**, incluida la que recorre la cadena entera contra SQL Server real: guardar,
  interceptor, despachador, handler y aviso a la pantalla
- 🔁 **Sin hub de SignalR propio** (ADR-0027). Toda pantalla de TopCut es una página de Blazor
  Server y **ya tiene su WebSocket**; un hub aparte serían dos conexiones por espectador cuando el
  plan gratuito de App Service permite cinco en total. El hub sigue teniendo sentido para clientes
  **externos** (la app móvil), y va con la Web API externa del hito 8
- ⏸ **Pantalla móvil del jugador para reportar en un par de toques**: necesita que un jugador este
  vinculado a su participante, y ese vínculo lo crea la inscripción, que es el hito 5. Pendiente de
  confirmar si se hace al principio del hito 5 o se adelanta atando la cuenta a mano

### Hito 5 — Cara pública: búsqueda, inscripción y lista de espera ⬜

Búsqueda por ciudad / tienda / TCG / fecha en SSR estático, inscripción y baja, **lista de espera
con promoción automática** (con concurrencia optimista al ocupar la última plaza), historial del
jugador.

### Hito 6 — Ligas por temporada ⬜

Acumulación de puntos entre torneos reutilizando el modelo de puntuación, clasificación de liga.

### Hito 7 — Panel de Superadmin ⬜

Aprobar y revocar el rol de tienda, catálogo de TCGs, tiempos de ronda por defecto de la plataforma.

### Hito 8 — Web API externa, despliegue y CI/CD ⬜

Endpoints REST con Bearer y [`api.md`](api.md) al día. Despliegue en App Service F1 + Azure SQL
gratuito + Azure SignalR gratuito, y GitHub Actions con OpenID Connect.

## Fase 2 — App Android nativa (.apk) ⬜

Blazor Hybrid sobre .NET MAUI reutilizando los componentes MudBlazor, consumiendo la Web API con
tokens Bearer. Aquí entran las **notificaciones push** (Firebase Cloud Messaging), añadiendo
handlers a los eventos de dominio ya existentes.

## Fase 3 — Publicación en tiendas ⬜

Google Play y, si es viable, iOS.

## Fase 4 — Apertura a más tiendas y más TCGs ⬜

Pokémon, Yu-Gi-Oh, Digimon... Sin cambios de arquitectura: dar de alta el ruleset desde el panel de
Superadmin. Aquí también entra la **geolocalización real** ("torneos cerca de mi"), aprovechando
los campos `Latitud`/`Longitud` reservados desde la v1.
