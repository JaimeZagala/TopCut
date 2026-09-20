# Arquitectura

> Ultima actualizacion: 2026-09-20 (hito 3a)

## Stack

| Pieza | Eleccion |
|---|---|
| Framework | .NET 10 (LTS, soporte hasta noviembre de 2028) |
| Web | ASP.NET Core 10 + Blazor Web App (modelo unificado) |
| API | ASP.NET Core Web API (controladores REST) + SignalR |
| Datos | Entity Framework Core 10 sobre SQL Server / Azure SQL |
| Identidad | ASP.NET Core Identity (email+contrasena y Google) |
| UI | MudBlazor |
| Futuro movil | .NET MAUI 10 + Blazor Hybrid |

## Capas

```
┌──────────────────────────────────────────────────────┐
│      Presentation — UN SOLO PROCESO (ADR-0004)       │
│  ┌──────────────────┐  aloja  ┌──────────────────┐   │
│  │   TopCut.Web     │────────▶│   TopCut.Api     │   │
│  │  Blazor Web App  │         │  REST + SignalR  │   │
│  │  (SSR + Server)  │         │ (clientes ext.)  │   │
│  └────────┬─────────┘         └────────┬─────────┘   │
└───────────┼────────────────────────────┼─────────────┘
            │     llamada en proceso     │
┌───────────▼────────────────────────────▼─────────────┐
│                  TopCut.Application                  │
│   casos de uso: crear torneo, lanzar ronda,          │
│   registrar resultado, promover lista de espera      │
└───────────┬────────────────────────────┬─────────────┘
            │                            │
┌───────────▼──────────┐      ┌──────────▼─────────────┐
│    TopCut.Domain     │      │ TopCut.Infrastructure  │
│  entidades y reglas  ◄──────┤  EF Core, SQL Server,  │
│  puras, sin deps     │      │  Identity, externos    │
└──────────────────────┘      └────────────────────────┘
```

Reglas de dependencia:

- **Domain** no referencia nada. Entidades y reglas de negocio puras (torneo, ronda,
  emparejamiento, ruleset), mas el **motor**: `SwissPairer`, `StandingsCalculator`,
  `TopCutBracket` y `TournamentEngine`, que son funciones puras sobre el agregado y por eso se
  pueden probar simulando torneos enteros sin base de datos.
- **Application** referencia Domain. Casos de uso independientes de si se invocan desde Blazor o
  desde HTTP.
- **Infrastructure** referencia Application. EF Core, acceso a SQL Server, Identity, integraciones.
- **Presentation** referencia Application (para invocar casos de uso) e Infrastructure (solo como
  raiz de composicion, para registrar los servicios en el contenedor de DI).

Los componentes Blazor Server llaman a los casos de uso **directamente, en el mismo proceso**, sin
salto de red innecesario. La Web API expone **esos mismos casos de uso** por HTTP, pensada desde
ya para que la futura app movil los consuma sin duplicar logica.

**Topologia de alojamiento** (ADR-0004): `TopCut.Api` es una **biblioteca de clases** con los
controladores REST y los hubs de SignalR. `TopCut.Web` la referencia y la registra, de forma que la
UI Blazor y los endpoints `/api/*` se sirven desde **un unico proceso y un unico despliegue**: un
solo cold start, una sola configuracion de Identity y sin CORS que gestionar. Separarlos mas
adelante seria anadir un host minimo que referencie la misma biblioteca.

## Modos de renderizado de Blazor

La interactividad es **global** y las paginas que deben servirse estaticas se excluyen una a una con
`[ExcludeFromInteractiveRouting]` (ADR-0020). Al reves no se puede: un *layout* no hereda el modo de
render de su pagina, asi que con interactividad pagina a pagina la cascara se queda muerta.

| Pantalla | Modo | Motivo |
|---|---|---|
| Portada, aviso de privacidad, errores | SSR estatico | No gastan una conexion de SignalR en alguien que solo lee |
| Registro, acceso, alta con Google | SSR estatico | Entrar escribe la cookie, y eso necesita el `HttpContext` de una peticion real |
| Busqueda publica de torneos (hito 5) | SSR estatico | Rapido, indexable, sin sesion ni circuito |
| Gestion de torneo, pantalla en vivo, inscripcion | `InteractiveServer` | Necesitan interactividad y tiempo real |

Dos cascaras: `MainLayout` (publica, sin nada que dependa de un circuito abierto) y `StoreLayout`
(gestion, con cajon lateral, dialogos y avisos emergentes).

Los formularios de las pantallas estaticas necesitan que cada campo lleve su `name` para que el
servidor los recoja del POST; con MudBlazor eso hay que ponerlo a mano (ADR-0021), y esta
encapsulado en `SsrTextField` y `SsrCheckBox`.

## Tiempo real

Las pantallas de la web **ya hablan por SignalR**: cada pagina de Blazor Server tiene su circuito.
Las actualizaciones en vivo viajan por ahi (ADR-0027) en vez de por un hub propio, que significaria
dos WebSockets por espectador cuando el plan gratuito permite cinco.

- **Quien avisa**: un evento de dominio, despachado despues del commit, llega a un handler que
  publica en un difusor en memoria. El aviso **no lleva datos**: dice que el torneo se movio, y cada
  pantalla vuelve a leer con la consulta que ya usa.
- **Pantalla en vivo del organizador** (emparejamientos y resultados segun entran, clasificacion
  entre rondas), pensada para proyectar en la sala.
- **Cronometro de ronda** (ADR-0028): el servidor manda el instante de fin en UTC **y su propia
  hora**; el navegador mide la diferencia una vez y cuenta el solo. Recargar no reinicia el reloj y
  no se gasta un mensaje por segundo y espectador.
- **Un hub propio** queda para los clientes **externos** (la app movil, que no tiene circuito), con
  la Web API externa.

En produccion se delega en **Azure SignalR Service** (ver mas abajo): lo que se delega son los
circuitos de Blazor, que es justo lo que resuelve el limite de WebSockets del plan gratuito.

> El difusor es de **un solo proceso**. Con varias instancias de App Service cada una avisaria solo
> a sus propias pantallas; el plan gratuito tiene una sola instancia, y el dia que haya mas esto
> pasa a ser un backplane detras de la misma interfaz.

## Acceso a datos

Dos caminos distintos, segun lo que haga falta (ADR-0026):

- **Cambiar algo**: `ITournamentRepository` carga el agregado entero, aplica la operacion y guarda
  **solo si sale bien**. Una regla rota a mitad no deja rastro.
- **Leer para pintar**: `ITournamentQueries` e `ITcgCatalog` devuelven proyecciones sin seguimiento,
  con los contadores calculados en SQL Server. Una lista de torneos no carga rondas ni partidas.

Cada operacion abre su **propio `DbContext`** (ADR-0023), creado por `IDbContextFactory` con el
mismo `ITenantContext` del ambito, no desde un ambito hijo: un ambito hijo traeria un contexto de
tenant vacio y el filtro, que falla cerrado, no devolveria nada.

## Multi-tenancy

Una **unica base de datos compartida** con columna `TenantId` (identificador de tienda) en las
entidades que pertenecen a una tienda, y **filtros de consulta globales de EF Core**
(`HasQueryFilter`) para que cada tienda solo vea sus propios datos automaticamente.

Motivo: es el enfoque mas simple, mas barato (una sola base de datos, que ademas encaja con el
limite del nivel gratuito de Azure SQL de una base de datos por suscripcion) y valido de sobra
para el volumen previsto.

Los filtros se aplican **por convencion**, recorriendo las entidades que implementan `ITenantOwned`
al construir el modelo, para que anadir una entidad nueva no pueda filtrar datos entre tiendas
porque alguien olvidase una linea. Y **fallan cerrado** (ADR-0011): sin tienda en contexto no se ve
nada, y leer entre tiendas exige `IgnoreQueryFilters()` explicito en la consulta. `Store` no lleva
filtro porque una tienda *es* el tenant.

De donde sale la tienda en contexto depende de por donde entre la peticion (ADR-0024): del claim del
`HttpContext` en una peticion HTTP, y fijada por `StoreLayout` desde el principal autenticado cuando
se trata de una pagina interactiva, porque **dentro de un circuito de SignalR no hay `HttpContext`**.

## Autenticacion

- **Blazor Web App**: cookie de sesion de ASP.NET Core Identity.
- **Web API para clientes externos** (futura app movil): **tokens Bearer**, independientes de la
  cookie. Se apoyara en los endpoints de Identity API de ASP.NET Core.
- **Login social**: proveedor externo de Google.
- Los tres roles (Superadmin, Tienda, Jugador) son roles de Identity combinados con politicas de
  autorizacion.
- **Cada puerta acepta una sola llave** (ADR-0013 y ADR-0022): las politicas nombradas que usan las
  paginas se autentican solo con la cookie, y los endpoints `/api/v1/*` solo con Bearer. Una
  politica que aceptase las dos responderia a un acceso denegado con una redireccion y un 401 a la
  vez.
- Quien no tiene sesion acaba en `/cuenta/acceso?volver=...` y quien no tiene permiso en
  `/sin-permiso`. El destino de vuelta se valida: solo rutas dentro del sitio.

## Concurrencia

Dos cosas distintas con el mismo nombre. La primera, **no compartir un `DbContext` entre
operaciones simultaneas**: Blazor renderiza varios componentes en paralelo y un contexto no admite
dos consultas a la vez. La comprobacion de tienda aprobada ya abre su propio ambito por consulta
(ADR-0023), y el acceso a datos de las pantallas interactivas tendra que hacer lo mismo.

La segunda, control de concurrencia **optimista** (columna `RowVersion` de EF Core) en las
operaciones sensibles a condiciones de carrera:

- Reportar el resultado de una partida.
- Ocupar la ultima plaza de un torneo.
- Promover a alguien desde la lista de espera.

Cuando salta, el choque se devuelve como un mensaje que el organizador entiende ("alguien ha
cambiado este torneo mientras lo editabas"), no como un error de servidor: dos personas llevando el
mismo torneo desde dos dispositivos es una situacion normal en una tienda, no una averia.

## Eventos de dominio

Los eventos de negocio (`RoundStarted`, `ResultRecorded`, `RoundTimeExpired`, `SeatFreed`,
`TournamentCompleted`) se publican como eventos internos desacoplados. Un interceptor de EF Core los
recoge de las entidades rastreadas y los despacha **despues** de que el guardado haya tenido exito,
de modo que ningun handler reacciona a trabajo que se revirtio.

El despachador es propio, de unas treinta lineas, en vez de MediatR (ADR-0010). En la v1 **no hay
ningun suscriptor**; activar push (Firebase Cloud Messaging en Android) o Web Push en el futuro
sera anadir un handler, no tocar la logica de negocio.

## Preparado para el futuro sin construirlo ahora

- **Geolocalizacion**: la direccion de la tienda se guarda **estructurada** (calle, ciudad,
  provincia, codigo postal) desde la v1, con campos `Latitude`/`Longitude` **opcionales y sin uso**.
  Activar "torneos cerca de mi" sera anadir una consulta por distancia, no rediseniar el modelo.
- **App Android**: Blazor Hybrid sobre .NET MAUI, reutilizando los componentes MudBlazor de la web.
  El workload `maui-windows` ya esta instalado en el equipo de desarrollo.
- **Mas tiendas y mas TCGs**: no requiere cambios de arquitectura, solo dar de alta el tenant o el
  ruleset desde el panel de Superadmin.

## Localizacion

El contenido de la v1 es **solo en espanol**, pero los textos de interfaz van en archivos de
recursos **`.resx`** desde el principio, para que internacionalizar mas adelante sea configurar un
idioma mas y no reescribir vistas. Por eso `InvariantGlobalization` esta desactivado en
[`Directory.Build.props`](../Directory.Build.props).

En concreto: `src/TopCut.Web/Resources/Textos.resx`, del que **MSBuild genera una clase fuertemente
tipada en cada compilacion** (ADR-0019), asi que se escribe `@Textos.HomeTitle` y una clave mal
escrita no compila. Anadir un idioma sera anadir `Textos.<cultura>.resx` al lado y la cultura en
`Program.cs`. El aviso de proteccion de datos **no** esta ahi: es contenido legal versionado.

## Infraestructura y despliegue (gratuito)

Proyecto personal: todo se despliega en recursos gratuitos de una cuenta propia.

| Recurso | Plan | Limites relevantes |
|---|---|---|
| **Azure App Service** | Gratuito (F1) | Sin "Always On" (cold start tras inactividad), cuota de CPU compartida al dia, sin dominio propio con SSL (solo `*.azurewebsites.net`, que si trae HTTPS) |
| **Azure SignalR Service** | Gratuito | ~20 conexiones concurrentes, ~20.000 mensajes/dia, 1 unit |
| **Azure SQL Database** | Oferta gratuita | Serverless, ~100.000 segundos-vCore/mes y 32 GB, **una base de datos gratuita por suscripcion** |
| **CI/CD** | GitHub Actions | Minutos gratuitos de sobra para un repo personal; despliegue via OpenID Connect, sin credenciales de larga duracion |

**Por que Azure SignalR Service y no SignalR directo sobre App Service**: el plan gratuito de App
Service limita las conexiones WebSocket a **5 simultaneas**, insuficiente para una pantalla en vivo
con 12-16 jugadores conectados. Delegar el tiempo real en Azure SignalR Service (modo "Default")
elimina ese cuello de botella y entra de sobra en el volumen de un torneo local.

> Los niveles gratuitos de los proveedores cloud cambian con frecuencia: **revisar los limites
> exactos en el momento de desplegar**.

### Alternativa anotada

Si el nivel gratuito de Azure se queda corto (por ejemplo, si el cold start de App Service resulta
molesto): **Render** o **Railway** ofrecen niveles gratuitos para contenedores .NET con mejor
comportamiento "always-on" limitado, pero exigirian moverse de SQL Server a **PostgreSQL** para
aprovechar sus niveles gratuitos de base de datos. No hay que planificarlo ahora; queda anotado.

Si el cold start molesta pero se quiere seguir en Azure, el siguiente escalon (App Service Basic
B1) tiene un coste mensual bajo.

## Entornos y aislamiento de la base de datos

El nivel gratuito de Azure SQL permite **una sola base de datos por suscripcion**, asi que no hay
un "staging" completo sin coste. Tres capas, todas gratuitas:

1. **Desarrollo local** — **SQL Server en Docker**, con el `compose.yaml` de la raiz (ADR-0015).
   Ningun cambio se prueba primero contra un recurso de la nube.
2. **Pruebas de integracion automatizadas** — **Testcontainers para .NET**
   (`Testcontainers.MsSql`), que levanta una instancia real y desechable de SQL Server en cada
   ejecucion. **No** el proveedor InMemory de EF Core, que no valida las mismas restricciones que
   SQL Server. Se ejecutan en GitHub Actions en cada pull request, antes de fusionar cambios de
   esquema.
3. **Antes de tocar produccion** — generar el script SQL de cada migracion y revisarlo a mano antes
   de aplicarlo contra la base real, ya que no hay una base de respaldo donde ensayarlo. Los
   scripts se versionan en [`migraciones/`](migraciones/):

   ```
   dotnet dotnet-ef migrations script --idempotent --project src/TopCut.Infrastructure --output docs/migraciones/00X-Nombre.sql
   ```

   Se generan **idempotentes**, de modo que aplicar uno dos veces no rompe nada.

Si Docker no esta disponible en la maquina, la variable `TOPCUT_TEST_SQL` apunta la bateria de
integracion a un SQL Server existente, LocalDB incluido (ADR-0012). Es una valvula de escape para
seguir trabajando, no un sustituto: un cambio de esquema se valida con Testcontainers antes de
fusionarse.

Si hace falta un entorno mas parecido a produccion para pruebas manuales, se usa una **tienda de
pruebas dentro de la misma base de datos** — un tenant marcado como "prueba" y excluido de las
busquedas publicas — en lugar de duplicar infraestructura.

## UI

**MudBlazor** (version 9) como libreria principal de componentes: es la mas usada en Blazor (mas
ejemplos y documentacion), sigue Material Design con estetica coherente sin necesitar disenador,
funciona igual en Blazor Server y en Blazor Hybrid (.NET MAUI), y es gratuita y de codigo abierto.

El tema esta en `Theme/TopCutTheme.cs`, uno solo para toda la aplicacion, con paleta clara y oscura.
**No se carga ninguna fuente externa**: una tipografia servida desde un CDN enviaria la IP de cada
visitante a un tercero al cargar la pagina, lo cual queda raro en la misma aplicacion que ensena un
aviso de proteccion de datos. Se usa la pila de fuentes del sistema.

Alternativa anotada: **Radzen Blazor** (140+ componentes, tambien compatible con MAUI Blazor
Hybrid) si en algun momento hacen falta tablas de datos muy densas, por ejemplo en el panel de
Superadmin. No se mezclan en la v1.

Pautas de diseno:

- Prioridad a **claridad y velocidad de uso en la pantalla en vivo del organizador**: es la que se
  usa bajo presion, en mitad de un torneo con la sala esperando.
- En movil, **reportar resultado debe completarse en un par de toques**.
- Las fechas y horas que teclea la tienda se interpretan en una **zona horaria configurada**
  (ADR-0025), no en la de la maquina: en produccion el servidor corre en UTC.
- Identidad visual simple y neutra en la v1; branding por tienda queda como mejora futura.
