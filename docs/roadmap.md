# Roadmap

> Última actualización: 2026-09-21
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

### Hito 1 — Modelo de datos, multi-tenancy e identidad ✅

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

### Hito 4 — Tiempo real: pantalla en vivo y cronómetro ✅

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
- ✅ **Pantalla móvil del jugador para reportar en un par de toques**: necesitaba que un jugador
  estuviera vinculado a su participante, y ese vínculo lo crea la inscripción. Hecha en el tramo
  **5c**, en cuanto el 5b creó el vínculo

### Hito 5 — Cara pública: búsqueda, inscripción y lista de espera ✅

Partido en tres tramos. El último recoge lo que quedó pendiente del hito 4, porque la pantalla del
jugador necesita el vínculo entre cuenta y participante que crea la inscripción.

#### 5a — Búsqueda pública y ficha del torneo ✅

- ✅ **`/torneos`**: búsqueda por texto (nombre del torneo o de la tienda), juego, ciudad y rango de
  fechas, con los próximos por defecto y los ya jugados a un desplegable de distancia
- ✅ **Formulario GET en SSR estático**: la búsqueda queda en la barra de direcciones, así que se
  puede guardar en favoritos, compartir y volver atrás; y mirar torneos no gasta una de las ~20
  conexiones del nivel gratuito de Azure SignalR (ADR-0020)
- ✅ **`/torneos/{id}`**: ficha con cuándo, dónde, plazas y **las reglas con las que se va a jugar**,
  que son la copia que el torneo guardó al crearse y no el reglamento de hoy
- ✅ **La única lectura que cruza tiendas** (ADR-0029), con el filtro de tenant apagado en un solo
  sitio y sustituido por una regla explícita: torneo publicado, tienda aprobada **ahora** y que no
  sea tienda de pruebas. Un torneo cancelado sigue viéndose: alguien lo tenía en su agenda
- ✅ **404 de verdad** para un enlace que no lleva a ningún torneo público (ADR-0030)
- ✅ Corregido de paso: el listado de la tienda formateaba la fecha con la zona horaria de la
  máquina en vez de la configurada (ADR-0025), que en Azure habría sido UTC
- ✅ **20 pruebas nuevas**: 14 sobre que se ve y que no se ve entre tiendas contra SQL Server real, y
  6 que recorren las dos pantallas por HTTP

#### 5b — Inscripción, baja y lista de espera ✅

- ✅ **Apuntarse y darse de baja** desde la ficha pública, con la cuenta del jugador. Dos
  formularios POST normales, sin circuito: el panel vuelve a leer el estado y lo enseña ("estás el
  3.º de la lista"), que dice más que cualquier mensaje de confirmación
- ✅ **Lista de espera con promoción automática**: al darse de baja alguien con plaza, entra el
  primero de la cola y **la cola se renumera entera**. Corregido de paso un fallo que nadie podía
  ver mientras la lista solo la miraba la tienda: quien salía por la mitad dejaba su número atrás
- ✅ **Una cuenta, una fila**. Quien se borra y vuelve reincorpora su misma inscripción en vez de
  crear otra, y no recupera la plaza si ya se la dieron a quien esperaba
- ✅ **La última plaza ya no se vende dos veces** (ADR-0032). Dos inscripciones simultáneas no
  chocaban porque la fila del torneo no cambiaba; ahora la versión de la raíz protege todo el
  agregado, y la prueba monta la carrera de verdad contra SQL Server
- ✅ **La escritura entre tiendas entra por una puerta con nombre** (ADR-0031),
  `ExecuteOnPublicAsync`, con la misma regla de visibilidad que la búsqueda y sin tocar el ámbito
  de tienda de la petición
- ✅ **Baja solo antes de empezar**: con el torneo en marcha cambia los emparejamientos, así que
  pasa a ser cosa de la mesa de control
- ✅ **`/mis-torneos`**: los torneos del jugador, de todas las tiendas donde haya jugado, con
  próximos y jugados
- ✅ **Las pantallas de la tienda se enteran**: `ParticipantRegistered` y `ParticipantDropped`
  sustituyen a `SeatFreed`, que solo cubría la mitad de los casos
- ✅ **20 pruebas nuevas**: 4 de dominio, 11 sobre el camino del jugador contra SQL Server real
  (carrera por la última plaza incluida) y 6 que recorren las pantallas por HTTP

#### 5c — Pantalla móvil del jugador ✅

Lo que quedaba del hito 4, hecho en cuanto la inscripción del 5b creó el vínculo entre cuenta y
participante.

- ✅ **`/torneos/{id}/mi-mesa`**: su mesa, su rival, el reloj de la ronda y cómo va en la
  clasificación, en una pantalla pensada para un móvil encima de la mesa
- ✅ **Reportar en un toque**, con los botones leídos **desde el jugador** ("Gano 2-1", no "1-2") y
  en el mismo orden para los dos lados de la mesa (ADR-0034)
- ✅ **Se reporta una sola vez**: el primero cierra la partida y las discrepancias las arregla la
  tienda, como un acta firmada. La mesa que se reporta se comprueba en el servidor, porque el
  identificador viaja en un formulario
- ✅ **Sin circuito** (ADR-0033). Es la única pantalla en vivo que se multiplica por jugador, y el
  plan gratuito da cinco WebSockets en total. El reloj lo arranca un módulo desde la propia página
  y, mientras no haya nada que pulsar, la página se refresca sola cada 25 segundos
- ✅ El bye llega ya resuelto y lo dice; la lista de espera y las bajas también tienen su mensaje
- ✅ **13 pruebas nuevas**: 7 sobre la mesa del jugador contra SQL Server real (incluida la que
  comprueba que lo que reporta el jugador B se guarda como victoria de B) y 6 sobre la pantalla
  por HTTP

### Hito 6 — Ligas por temporada ✅

Partido en cuatro tramos. Los dos primeros construyen a **quién** se le suman los puntos, que es lo
que faltaba: hasta ahora un participante era un nombre suelto de un torneo.

#### 6a — Fichero de jugadores de la tienda ✅

- ✅ **`StorePlayer`** (ADR-0035): quien ha jugado en la tienda. Cada participante apunta a una
  ficha, y la ficha es lo que una liga puede sumar
- ✅ **El mostrador sigue siendo escribir e Intro**, pero ahora enseña a quién va a apuntar: las
  fichas que encajan con lo tecleado, con cuántos torneos lleva cada una
- ✅ **Dos puertas distintas a propósito**: lo tecleado en el mostrador se busca en el fichero de esa
  tienda; una inscripción por web empareja **solo por cuenta**, nunca por nombre, porque ahí no hay
  nadie en la sala para notar que se ha fundido con quien no era
- ✅ **Botón "Es otra persona"** para la tienda que de verdad tiene dos Anas, y por eso teclear el
  mismo nombre dos veces responde "ya está inscrito" en vez de duplicar en silencio
- ✅ **`/tienda/jugadores`**: el fichero, con torneos jugados, última vez, quién tiene cuenta y qué
  fichas parecen duplicadas. Corregir un nombre cambia el fichero, no los torneos ya jugados
- ✅ **Migración con relleno**: los participantes que ya existían reciben su ficha con la misma regla,
  normalización de acentos incluida. Verificado sobre los datos reales de desarrollo
- ✅ Corregido de paso: el agregado cargaba los **participantes sin orden**, así que el mismo torneo
  podía emparejar distinto de una carga a otra. Mismo fallo que el de las rondas del hito 4
- ✅ **12 pruebas nuevas**: 10 sobre quién se funde con quién contra SQL Server real y 2 sobre la
  pantalla del fichero

#### 6b — Fusionar fichas duplicadas ✅

- ✅ **Fusionar desde la pantalla del fichero**: la tienda elige cuál de las dos se queda, y todo lo
  que jugó la otra pasa a la superviviente. El diálogo avisa de que no tiene vuelta atrás
- ✅ **La cuenta viaja con la persona**: si la absorbida tenía cuenta y la superviviente no, se la
  lleva. Si **las dos** tienen cuenta, son dos personas y no se fusionan
- ✅ **Dos fichas que jugaron el mismo torneo tampoco**, porque dejarían a una persona dos veces en
  la misma acta. El mensaje dice qué torneo
- ✅ **Escrituras ordenadas y en una transacción.** El índice único no admite que las dos fichas
  tengan la cuenta ni por un instante, y EF no promete el orden de los dos `UPDATE` de un mismo
  guardado: la absorbida la suelta, llega a la base, y solo entonces la otra la toma
- ✅ **6 pruebas nuevas** sobre quién se fusiona y quién no, contra SQL Server real

#### 6c — Ligas ✅

- ✅ **`/tienda/ligas`**: crear una liga con su juego y su temporada, corregirla, cerrarla y volver a
  abrirla. Una liga cerrada no admite torneos nuevos; los que ya tenía siguen contando
- ✅ **La liga de un torneo se elige en su propia ficha**, encima del formulario y guardando al
  elegirla. Al crear un torneo se aterriza justo ahí, así que sigue siendo un clic
- ✅ **Asignar la liga ya no se congela al empezar el torneo** (ADR-0036): decide qué tabla suma los
  puntos, no toca nada de lo jugado. Sin eso, quien jugara tres torneos y luego decidiera que eran
  una temporada perdía esos puntos para siempre. Un torneo **cancelado** sí que se rechaza
- ✅ **Una sola respuesta a "¿a qué ligas puede ir este torneo?"**, la que pinta el desplegable y la
  que comprueba el servidor: abierta, del mismo juego y de la misma tienda, más la liga en la que ya
  estuviera aunque esté cerrada
- ✅ **12 pruebas nuevas** contra SQL Server real
- ✅ Corregido en el navegador: al elegir liga se guardaba bien pero el desplegable volvía a
  "Ninguna" hasta recargar, porque durante el guardado se le devolvía el valor viejo

#### 6d — Clasificación acumulada ✅

- ✅ **La clasificación de la temporada** se calcula con el **mismo calculador** que usa un torneo
  suelto, aplicado a cada torneo de la liga y sumando sus puntos por ficha del fichero (ADR-0037)
- ✅ **Los desempates oficiales no se suman**: un porcentaje de victorias de los rivales describe un
  torneo suizo, no una temporada. A igualdad de puntos mandan las partidas ganadas y, después,
  haberlo conseguido en menos torneos
- ✅ **Un torneo cancelado no puntúa** aunque se cancelara después de jugarse, y apuntarse a algo que
  nadie llegó a jugar no cuenta como torneo jugado
- ✅ **Página pública de la liga** en `/ligas/{id}`, en SSR estático y sin circuito, con la
  clasificación y los torneos que la han ido llenando. La ficha de cada torneo enlaza a su liga
- ✅ **Una liga es pública cuando alguno de sus torneos lo es**: la misma regla de ADR-0029
  preguntada a la liga, así que una temporada en borradores no tiene dirección que encontrar y
  revocar una tienda se lleva su liga en la siguiente petición
- ✅ **Se calcula al leer, sin tabla materializada**: corregir un resultado del mes pasado cambia la
  clasificación en la siguiente lectura. Los agregados se cargan en una sola tanda de consultas
- ✅ **13 pruebas nuevas**: 6 de dominio sobre cómo se suma y cómo se desempata, y 7 contra SQL
  Server real sobre el fichero, la visibilidad pública y el 404 de una liga sin publicar

#### 6e — El jugador ve sus ligas ✅

- ✅ **`/mis-ligas`**: las ligas en las que el jugador ha puntuado, de cualquier tienda donde juegue,
  con su puesto, sus puntos y cuántos torneos lleva. Lo pedía la visión y lo prometía el aviso de
  privacidad, y no estaba
- ✅ **La clasificación marca tu fila** cuando la miras con la sesión abierta, con fondo y con una
  etiqueta al lado del nombre: el color solo no vale para quien no lo distingue. Quién lee sale del
  principal autenticado, nunca de la dirección
- ✅ **La página pública de una liga lleva `noindex`**. Es la única página de TopCut que publica
  nombres de personas: la ficha de un torneo enseña cuántos inscritos hay, no quiénes. Se comparte
  por enlace a propósito, pero no sale al buscar el nombre de alguien. `noindex` y no `robots.txt`:
  prohibir el rastreo impediría leer precisamente esa etiqueta
- ✅ Una liga en la que te has apuntado pero no has jugado todavía no aparece: no hay nada que
  clasificar
- ✅ **6 pruebas nuevas**: 4 contra SQL Server real sobre lo que ve el jugador y de qué tienda, y 2
  por HTTP sobre la página y su acceso

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
