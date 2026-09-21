# Registro de decisiones (ADR)

Cada decisión de diseño no trivial se anota aquí con fecha, la decisión tomada, y la alternativa
descartada y por que. Si una decisión tomada en el chat contradice algo escrito en `/docs`, se
actualiza el documento afectado y se añade una entrada nueva aquí.

---

## ADR-0001 — Formato de archivo de solución: `.slnx` en vez de `.sln`

**Fecha**: 2026-09-19 · **Estado**: aceptada · **Impacto**: bajo, reversible en un comando

El brief pedía `TopCut.sln`. `dotnet new sln` en el SDK 10 genera por defecto el formato **`.slnx`**
(XML, legible, sin GUIDs duplicados, mucho menos ruidoso en los diffs). Se conserva el default:
**`TopCut.slnx`**.

- **Alternativa descartada**: forzar `.sln` clásico con `dotnet new sln --format sln`.
- **Por qué**: `.slnx` está soportado por la CLI de .NET 10 y por Visual Studio 2022 17.14+ / VS 18
  (el equipo tiene VS 18.10). El formato clásico solo aportaría compatibilidad con herramientas
  antiguas que aquí no se usan.
- **Cómo revertir**: `dotnet sln migrate` en sentido contrario, o regenerar con `--format sln` y
  volver a añadir los proyectos. Cinco minutos.

---

## ADR-0002 — Propiedades y versiones de paquetes centralizadas

**Fecha**: 2026-09-19 · **Estado**: aceptada · **Impacto**: bajo, reversible

Se añaden `Directory.Build.props` (TargetFramework, nullable, implicit usings, `LangVersion`,
`InvariantGlobalization=false`) y `Directory.Packages.props` (Central Package Management: los
`.csproj` declaran `PackageReference` **sin** `Version`).

- **Alternativa descartada**: repetir las propiedades y versiones en cada `.csproj`.
- **Por qué**: con 7 proyectos y creciendo, una sola versión por paquete evita desalineaciones
  silenciosas entre capas, que es una fuente clásica de errores de runtime con EF Core y ASP.NET.
  Es más barato adoptarlo ahora (3 proyectos con paquetes) que más tarde.

---

## ADR-0003 — La plantilla de Blazor se generó sin el scaffolding de Identity (`--auth None`)

**Fecha**: 2026-09-19 · **Estado**: aceptada · **Impacto**: medio, reversible con trabajo manual

`dotnet new blazor -au Individual` habría generado un `ApplicationDbContext` y las páginas de
Identity **dentro de `TopCut.Web`**, lo que rompe la regla de capas (EF Core e Identity pertenecen
a `TopCut.Infrastructure`). Se genero con `-au None` y la identidad se cableará a mano en el
hito 1, con el `DbContext` en Infrastructure.

- **Alternativa descartada**: usar el scaffolding y luego mover los archivos de capa.
- **Por qué**: mover el scaffolding después arrastra rutas, namespaces y `_Imports` a medio migrar;
  cablearlo limpio desde el principio sale más barato y deja la capa correcta desde el commit 1.

---

## ADR-0004 — `TopCut.Api` es una biblioteca alojada por `TopCut.Web` (proceso único)

**Fecha**: 2026-09-19 · **Estado**: aceptada (confirmada por Jaime) · **Impacto**: medio

`TopCut.Api` es una **biblioteca de clases** con los controladores REST y los hubs de SignalR.
`TopCut.Web` la referencia y la registra (`AddControllers().AddApplicationPart(...)`), de modo que
la UI Blazor y los endpoints `/api/*` se sirven desde **un único proceso y un único despliegue**.

- **Alternativas descartadas**:
  - *Dos App Services separados*: dos cold starts (y en el F1 gratuito el cold start ya duele),
    dos configuraciones de Identity, y CORS que gestionar. El escalado independiente que aportaría
    es irrelevante al volumen previsto (una tienda, 12-32 jugadores por torneo).
  - *Controladores dentro de `TopCut.Web`, sin proyecto `TopCut.Api`*: más simple, pero pierde la
    separación de namespaces que describe el brief y facilita que se cuele lógica de negocio en
    los controladores.
- **Por qué**: se conserva la frontera explicita entre "UI" y "API para clientes externos" a nivel
  de proyecto, sin pagar el coste operativo de dos despliegues. Si algún día hace falta separarlos,
  basta con añadir un host `TopCut.Api.Host` de 20 líneas que referencie la misma biblioteca.

---

## ADR-0005 — Herramientas del equipo de desarrollo

**Fecha**: 2026-09-19 · **Estado**: aceptada y ejecutada

Estado inicial del equipo: .NET SDK 10.0.401, SQL Server LocalDB (`MSSQLLocalDB`) y el workload
`maui-windows`; **sin git**, y con Docker Desktop **instalado pero roto**.

Decisión (confirmada por Jaime): instalar **git y Docker Desktop**.

- **git 2.55.0.3**: instalado vía `winget` en `C:\Program Files\Git`. Desbloquea el control de
  versiones, el repositorio en GitHub y el CI/CD del hito 8.
- **Docker Desktop 29.8.0**: había una instalación obsoleta y rota (v26.0.0, con el daemon caído y
  sin su distro WSL). Jaime la reinstaló; el daemon corre con **contenedores Linux**, que es lo que
  Testcontainers necesita.
- **GitHub CLI 2.101.0**: instalado para crear y gestionar el repositorio remoto.
- **Alternativa descartada**: pruebas de integración contra LocalDB en local y Testcontainers solo
  en CI. Descartada porque LocalDB y Azure SQL difieren en restricciones y collation, y el objetivo
  de estas pruebas es precisamente validar el esquema contra un SQL Server real antes del PR.

Nota operativa: `git`, `docker` y `gh` se instalaron durante una sesión ya abierta, así que sus
rutas (`C:\Program Files\Git\cmd`, `C:\Program Files\Docker\Docker\resources\bin`,
`C:\Program Files\GitHub CLI`) no están en el `PATH` de terminales anteriores a la instalación.
Abrir una terminal nueva lo resuelve.

---

## ADR-0006 — Claves primarias: `int` para catálogos, `Guid` v7 para entidades de negocio

**Fecha**: 2026-09-19 · **Estado**: aceptada (confirmada por Jaime) · **Impacto**: alto, difícil de revertir

- **`int` autoincremental** en catálogos pequeños, estables y no expuestos en URLs públicas:
  `Tcg`, `Ruleset` y tablas de apoyo similares.
- **`Guid` secuencial v7** (`Guid.CreateVersion7()`, disponible desde .NET 9) en las entidades de
  negocio que aparecen en URLs o se sincronizan con clientes: `Tienda`, `Torneo`, `Participante`,
  `Ronda`, `Emparejamiento`, `Partida`, `Liga`.

- **Alternativas descartadas**:
  - *`int` en todo*: índices más compactos y URLs cortas, pero `/torneo/42` revela cuantos torneos
    lleva la plataforma — mal detalle en un producto que aspira a abrirse a otras tiendas — y
    complica fusionar datos si algún día se importa desde otra herramienta.
  - *`Guid` en todo*: uniforme, pero infla innecesariamente los índices de catálogos que nunca
    pasaran de una docena de filas.
- **Por qué v7 y no v4**: los GUID v7 son **monótonos en el tiempo**, así que se comportan como
  claves agrupadas secuenciales en SQL Server y no fragmentan el índice como haría un v4 aleatorio.
- **Nota para el hito 1**: los IDs de ASP.NET Core Identity (`AspNetUsers`, `AspNetRoles`) quedan
  como `string` — es el default del framework y cambiarlo aporta poco.

---

## ADR-0007 — Identidad en los commits y correo para los servicios del proyecto

**Fecha**: 2026-09-19 · **Estado**: aceptada

### Repositorio e identidad en los commits — decidido

El repositorio vive en **`JaimeZagala/TopCut`**, **privado**, bajo la cuenta personal de GitHub de
Jaime. Los commits se firman con la dirección `noreply` que GitHub proporciona:
`692421+JaimeZagala@users.noreply.github.com`.

- **Alternativas descartadas**:
  - *Repositorio público desde el principio*: el repo acabará conteniendo configuración de Azure y
    nombres de tiendas y jugadores reales. Privado → público es un clic; al reves no, porque lo
    indexado ya está fuera.
  - *Firmar con el Gmail personal*: en un repo que puede volverse público, el correo queda en cada
    commit para siempre y es rastreable por bots.
  - *Una segunda cuenta de GitHub para el proyecto*: fricción permanente (dos sesiones, dos claves,
    dos 2FA) y el código dejaría de contar como trabajo propio de Jaime.
- **Ejecución**: los dos commits que ya existían se reescribieron con `git commit-tree` antes del
  primer push, de modo que **el Gmail personal nunca llegó a GitHub**.

### Correo propio para los servicios — decidido

Se creo la cuenta **`topcut.tcgapp@gmail.com`**, que es la identidad de **Azure**, **Google Cloud**
(el OAuth del login con Google) y, en la fase 3, **Google Play Console**.

- **Por qué separarlo**: la cuenta de desarrollador de Play está atada a la cuenta que la crea y
  transferirla después es un tramite de soporte lento e incierto. Además, la oferta gratuita de
  Azure SQL es **una base de datos por suscripción**: una cuenta nueva estrena su propio cupo.
- **Por qué hacerlo antes del hito 1**: el login con Google necesita un proyecto de Google Cloud, y
  crearlo bajo la cuenta equivocada obliga a rehacerlo.
- **Riesgo a mitigar**: se convierte en un punto único de fallo (Azure + Google Cloud + Play). Exige
  2FA y correo de recuperación apuntando a la cuenta personal.
- **Lo que NO cambia**: GitHub sigue siendo la cuenta personal. El correo del proyecto se añade como
  email secundario verificado en esa misma cuenta si se quiere enrutar notificaciones.

---

## ADR-0008 — Código en inglés, textos visibles y documentación en español

**Fecha**: 2026-09-19 · **Estado**: aceptada (confirmada por Jaime) · **Impacto**: alto si se revierte tarde

Los identificadores y los comentarios del código van en **inglés** (`Tournament`, `Participant`,
`Pairing`, `Round`, `Ruleset`, `Store`). Se quedan en **español**:

- `/docs` al completo, que es lo que lee Jaime.
- Los **mensajes de `DomainException`**, porque pueden acabar en pantalla y el producto es
  monolingüe en español. Pasaran a recursos `.resx` en el hito 3, y entonces el código quedará
  limpio de cadenas traducibles.

- **Primera versión de este ADR** (descartada el mismo día): dominio en español para que el
  vocabulario del negocio, la documentación y el código usaran el mismo término.
- **Por qué se cambió**: convención más habitual y legible para cualquiera de fuera que mire el
  repositorio; encaja sin roces con el vocabulario de ASP.NET y EF Core; y evita identificadores
  con tildes o enes.
- **Coste real del cambio**: se ejecutó en el mismo hito 1a, sobre 19 archivos de dominio y 3 de
  pruebas. Hacerlo tres hitos más tarde habrían sido cientos.

## ADR-0009 — Fin de tiempo de ronda: política configurable, por defecto la oficial

**Fecha**: 2026-09-19 · **Estado**: aceptada (confirmada por Jaime)

El brief pedía que, al llegar el cronómetro a cero sin resultado reportado, el enfrentamiento se
marcase **automáticamente como empate**. Al validar los reglamentos oficiales apareció que **ambos
juegos dicen otra cosa**:

- **Magic** (MTR): tras los cinco turnos extra, gana el enfrentamiento **quien vaya por delante en
  partidas**; solo hay empate si van igualados.
- **One Piece** (TRM 5.2): tres turnos extra con límite de 5 minutos y, si sigue sin haber ganador,
  se resuelve por criterios de juego antes de declarar empate.

Con la regla literal del brief, un 1-0 en Bo3 al que se le acaba el tiempo se registraría como
empate cuando por reglamento es victoria.

**Decisión**: el ruleset lleva una política `TimeExpiryPolicy` con dos valores, y la tienda puede
cambiarla por torneo hasta que empiece:

| Valor | Comportamiento |
|---|---|
| `LeaderWins` (**por defecto en ambos TCG**) | Gana quien va por delante en partidas; empate solo si van igualados. Es lo que prescriben los dos reglamentos. |
| `AlwaysDraw` | Empate siempre, pase lo que pase. Es la regla literal del brief, disponible para quien prefiera cerrar rondas rápido y corregir a mano. |

**Consecuencia de diseño que esto obligó a resolver**: para que `LeaderWins` signifique algo hay
que saber quien iba por delante, y eso exige poder **registrar cada partida segun termina**, no
solo el resultado completo al final. De ahí `Tournament.RecordGame`, que añade una partida y cierra
el enfrentamiento solo en cuanto queda decidido. Encaja además con el requisito de que el jugador
pueda reportar en un par de toques desde el móvil.

En los dos casos el organizador puede corregir el resultado después, y el enfrentamiento queda
marcado con `DecidedByTimeExpiry` para que la pantalla en vivo pueda destacarlo.

---

## ADR-0010 — Despacho de eventos de dominio sin librería de mediador

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, reversible

El brief pedía publicar los eventos de dominio "mediante un mediador tipo MediatR". Se implementa
con un despachador propio de unas treinta líneas: `IDomainEventHandler<TEvent>` e
`IDomainEventDispatcher` en Application, y una implementación en Infrastructure que resuelve los
handlers registrados en el contenedor de DI. Un interceptor de EF Core recoge los eventos de las
entidades rastreadas y los pública **después** de que el guardado haya tenido éxito.

- **Alternativa descartada**: MediatR.
- **Por qué**: hay cinco tipos de evento, ningún suscriptor en la v1 y ninguna necesidad de
  pipeline de petición/respuesta. El despachador propio cubre exactamente eso y deja el proyecto sin
  una dependencia más que mantener.
- **Sobre la licencia**: MediatR 13 pasó a licencia comercial en julio de 2025, pero mantiene un
  tier Community gratuito por debajo de 5 M$ de facturación anual, así que **para este proyecto
  sería gratuito**. La decisión es de simplicidad, no de coste.
- **Cómo revertir**: si más adelante la capa Application quiere un pipeline de comandos con
  validación y logging transversales, cambiar a MediatR es sustituir la implementación del
  despachador; los handlers se renombran a `INotificationHandler` y poco más.

---

## ADR-0011 — El filtro de tenant falla cerrado

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: alto en seguridad, barato de mantener

El filtro global de EF Core es `entity.TenantId == context.CurrentTenantId`. Cuando no hay tienda en
contexto, la comparación es contra `NULL` y **no devuelve ninguna fila**.

- **Alternativa descartada**: que un contexto sin tenant vea todo, que es lo comodo para la búsqueda
  pública de torneos y para el panel de Superadmin.
- **Por qué**: con esa variante, cualquier fallo que deje el tenant sin resolver —un endpoint nuevo
  que olvide el middleware, un job en segundo plano, un test mal montado— expondria los datos de
  todas las tiendas en silencio. Con el filtro actual el mismo fallo produce una lista vacía: se ve
  enseguida y no filtra nada.
- **Consecuencia**: leer entre tiendas exige `IgnoreQueryFilters()` explicito en el sitio de la
  consulta. La búsqueda pública y el panel de Superadmin lo usaran, y al ser explicito queda
  visible en revisión de código en vez de ser el comportamiento por defecto.
- `Store` no lleva filtro: una tienda **es** el tenant, no pertenece a uno.

---

## ADR-0012 — Las pruebas de integración aceptan un SQL Server externo

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo

Por defecto la batería levanta SQL Server con Testcontainers, como fija el ADR-0005. Si la variable
de entorno `TOPCUT_TEST_SQL` trae una cadena de conexión, se usa esa instancia y se recrea el
esquema desde cero al arrancar.

- **Por qué**: desbloquea el desarrollo cuando Docker no está disponible en la máquina (LocalDB
  sirve de sustituto), y permite que un runner de CI que ya ofrece SQL Server como service
  container lo reutilice en vez de anidar un contenedor dentro de otro.
- **Lo que no cambia**: el camino por defecto sigue siendo Testcontainers, y es el que debe
  ejecutarse antes de fusionar un cambio de esquema. LocalDB y Azure SQL no son identicos.

---

## ADR-0013 — Los endpoints de la API se autentican solo con Bearer

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: seguridad

Las políticas con nombre (`IsSuperadmin`, `CanOrganize`, `IsPlayer`) aceptan **cookie y Bearer**,
porque las páginas Blazor las necesitan. Los endpoints bajo `/api/v1/*`, en cambio, exigen
**exclusivamente Bearer**.

- **Por qué**: si un endpoint de la API aceptase la cookie de sesión, un formulario alojado en otro
  sitio podría hacer que el navegador de la victima enviase la petición con su cookie adjunta, que
  es exactamente un CSRF. Con Bearer no hay riesgo: el token va en una cabecera que solo puede
  poner el cliente, nunca el navegador por su cuenta.
- **Coste**: ninguno. La UI Blazor **no llama a la API**: invoca los mismos servicios de
  `TopCut.Application` en el mismo proceso (ADR-0004). La API existe para clientes externos, que
  usan Bearer de todas formas.
- Los grupos de la API llevan además `DisableAntiforgery()`: son JSON y el middleware de
  antiforgery solo tiene sentido para formularios con cookie.

---

## ADR-0014 — Las páginas de error amables no se aplican a `/api`

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, pero corrige un fallo real

`UseStatusCodePagesWithReExecute` reejecuta la tuberia contra una página Razor para cualquier
respuesta de error sin cuerpo. Aplicado a toda la aplicación convertía un **401 de la API en un 400
con HTML**, inútil para un cliente que espera `problem+json`. Ahora va envuelto en un `UseWhen` que
excluye las rutas `/api`.

Lo detectaron las pruebas de integración del hito 1c, no una revisión del código: es el tipo de
fallo que solo aparece cuando algo llama de verdad al endpoint y mira el código de estado.

---

## ADR-0015 — La base de datos de desarrollo es SQL Server en Docker, no LocalDB

**Fecha**: 2026-09-20 · **Estado**: aceptada (confirmada por Jaime) · **Impacto**: bajo, solo afecta al puesto de desarrollo

El desarrollo local usa el SQL Server 2022 que levanta el [`compose.yaml`](../compose.yaml) de la
raíz, en el puerto **11433** y con los datos en un volumen con nombre. `appsettings.Development.json`
y el `DesignTimeDbContextFactory` apuntan ahí.

- **Alternativa descartada**: SQL Server LocalDB, que era la elección inicial del ADR-0005.
- **Por qué se cambió**:
  - **Es otro motor**. LocalDB es una variante de escritorio; producción es Azure SQL y las pruebas
    de integración ya usan el SQL Server de un contenedor. Con esto los tres entornos coinciden.
  - **Se apaga solo** cuando lleva unos minutos sin uso, y despertar tarda lo justo para perder la
    carrera contra el arranque de la aplicación. Eso es lo que obligó a escribir la espera del
    `DatabaseInitializer` — que se queda igualmente, porque **Azure SQL serverless también se
    pausa** y el problema es real en producción.
  - **Su estado vive en el registro de Windows** (`HKCU\SOFTWARE\Microsoft\Microsoft SQL Server\UserInstances`),
    separado de los ficheros en disco. Cuando las dos mitades se desincronizan, los errores que da
    apuntan a cualquier sitio menos a la causa: "no puedo crear el fichero porque ya existe"
    seguido de "la base ya existe" seguido de que la instancia entera deja de arrancar.
  - Un contenedor **se tira y se vuelve a levantar** (`docker compose down -v`) sin dejar residuos
    en el perfil del usuario ni en el registro.
- **Coste**: Docker Desktop pasa de ser opcional a requisito para desarrollar. Ya lo era en la
  práctica: sin el no se pueden ejecutar las pruebas de integración antes de fusionar un cambio de
  esquema (ADR-0005).
- **Lo que no cambia**: las pruebas de integración siguen usando Testcontainers, con su propio
  contenedor efímero. La base de desarrollo y la de las pruebas no se tocan entre si.

---

## ADR-0016 — Emparejamiento suizo por backtracking con preferencia Dutch

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: alto en corrección, contenido en una clase

El emparejador ordena a los jugadores por clasificación y los empareja recorriendo la lista con
**backtracking**. El orden en que prueba a los candidatos es lo que lo hace Dutch: el rival
preferido del primer jugador es el que le corresponde en la otra mitad de su grupo de puntuación, y
a partir de ahí prueba hacia los lados.

- **Alternativa descartada**: emparejar de una pasada, dividiendo cada grupo en dos mitades y
  cruzandolas directamente. Es lo que describe el método Dutch, y es más simple.
- **Por qué no**: esa pasada única falla cuando el cruce que toca ya se jugó. Entonces hay que
  improvisar intercambios, y con grupos impares que descuelgan jugadores al grupo siguiente el
  parcheo se vuelve un nido de casos. El backtracking **garantiza encontrar un emparejamiento sin
  repeticiones siempre que exista alguno**, y como explora en orden de preferencia Dutch, el
  resultado coincide con la pasada simple cuando está funcionaría.
- **Repeticiones**: se hace una primera pasada que las prohíbe por completo. Solo si esa pasada
  agota todas las combinaciones sin éxito se repite permitiendolas. Eso es literalmente "evitar
  repetir mientras existan alternativas": con 4 jugadores y 4 rondas no hay alternativa posible, y
  el torneo tiene que poder continuar igualmente.
- **Coste medido**: el backtracking es exponencial en el peor caso teórico, pero en la práctica la
  primera combinación que prueba suele valer, porque la preferencia Dutch es justamente la que casi
  siempre funciona. Medido sobre campos de 4 a 128 jugadores con las rondas que recomienda el
  apéndice E del MTR:

  | Jugadores | Rondas | Tiempo |
  |---:|---:|---:|
  | 7 | 3 | < 1 ms |
  | 19 | 5 | < 1 ms |
  | 27 | 5 | < 1 ms |
  | 32 | 5 | < 1 ms |
  | 37 | 6 | 39 ms |
  | 64 | 6 | 2 ms |
  | 101 | 7 | 7 ms |
  | 128 | 7 | 10 ms |

  Lo que encarece la búsqueda **no es el tamaño del campo sino cuantas parejas quedan libres**: 37
  jugadores tardan cuatro veces más que 128 porque con un campo impar mediano y seis rondas las
  combinaciones disponibles escasean y hay que retroceder de verdad. Aún así son milisegundos.

  El caso límite es el round robin completo, donde solo existe una familia de horarios válidos:
  10 jugadores en 9 rondas, o 7 jugadores en 7 rondas con un bye cada uno, se resuelven en menos de
  1 ms y sin una sola repetición.

---

## ADR-0017 — El enfrentamiento directo solo desempata a dos jugadores aislados

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, pero es una limitación consciente

El criterio `HeadToHead` solo se aplica cuando quedan **exactamente dos** jugadores igualados en
todo lo anterior y no hay nadie más con sus mismos puntos. En grupos de tres o más, no se aplica.

- **Por qué**: el enfrentamiento directo **puede ser circular**. Si A ganó a B, B ganó a C y C ganó
  a A, no existe ningún orden coherente que respete los tres resultados, y cualquier algoritmo que
  lo intente acaba dependiendo del orden en que mire las parejas.
- **Que dice el reglamento**: el manual de One Piece envia esos casos a **jugar otra partida** y, si
  no hay tiempo, a un método aleatorio decidido por el juez. Es decir, reconoce que el algoritmo se
  acaba ahí.
- **Consecuencia práctica**: un triple empate exacto queda en el orden que dejaron los criterios
  numéricos, y el organizador decide. Es lo correcto, no una carencia: fabricar un desempate donde
  el reglamento dice que no lo hay sería peor que no fabricarlo.

---

## ADR-0018 — La tabla de rondas por asistencia también es dato del ruleset

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, una columna y una migración

Cuantas rondas suizas jugar y de que tamaño hacer el top cut segun la asistencia **no es una
formula universal**: cada TCG pública su propia tabla, y no coinciden.

| Asistencia | Magic (MTR apéndice E) | One Piece (TRM 3.6) |
|---|---|---|
| 4-8 | 3 rondas, sin cut | 3 rondas, sin cut |
| 9-16 | 5 rondas, **Top 4** | **4 rondas**, **Top 2** |
| 17-32 | 5 rondas, **Top 8** | 5 rondas, **Top 4** |
| 33-64 | 6 rondas, Top 8 | 6 rondas, Top 8 |
| 65-128 | 7 rondas, Top 8 | 7 rondas, Top 8 |
| 129-226 / 129-256 | 8 rondas, **Top 8** | 8 rondas, **Top 16** |
| 227-409 / 257-512 | 9 rondas, Top 8 | 9 rondas, Top 16 |
| 410+ / 513-1024 | 10 rondas, Top 8 | 10 rondas, **Top 32** |

- **Alternativa descartada**: una función compartida `RecommendedRounds(jugadores)`. Se acerca
  bastante a `techo(log2(n))`, que es el principio del sistema suizo, pero **se equivoca en las
  bandas medias y en el tamaño del cut**, que es justo donde se juega la mayoría de los torneos de
  tienda. Con 12 jugadores propondría lo mismo a los dos juegos, y los dos reglamentos piden cosas
  distintas.
- **Decisión**: `Ruleset.AttendanceBands`, una lista ordenada de bandas con su límite superior, sus
  rondas y su tamaño de cut opcional, almacenada en una columna compacta igual que los criterios de
  desempate. `RecommendationFor(jugadores)` devuelve la banda que toca.
- **Es una sugerencia, no una imposición**: la tienda puede poner las rondas y el cut que quiera. El
  valor está en que la pantalla de crear torneo llegue con el número correcto ya puesto, porque
  elegir tres rondas para 27 jugadores deja varios invictos y decide el torneo por desempate en vez
  de por partidas.
- **Migración**: el seed rellena la tabla en los rulesets que ya existan y esten vacíos, pero nunca
  pisa una que el Superadmin haya ajustado.

---

## ADR-0019 — Los textos de interfaz salen de un `.resx` con clase generada en la compilación

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, afecta a las vistas

El brief pide que los textos visibles vivan en archivos de recursos desde el principio aunque la v1
sea solo en español. Se usa **`Resources/Textos.resx`** y se accede como `@Textos.HomeTitle`.

- **Alternativa descartada**: `IStringLocalizer<T>` con claves en cadena (`L["HomeTitle"]`), que es
  lo idiomatico en ASP.NET Core. Se descarta porque una clave mal escrita no falla: pinta la propia
  clave en pantalla y nadie se entera hasta que lo ve un usuario.
- **Alternativa descartada**: `.Designer.cs` generado por Visual Studio y versionado. Se descarta
  porque solo se regenera desde el IDE, y este proyecto se edita también desde la línea de comandos:
  el archivo acabaría desincronizado del `.resx`.
- **Decisión**: la clase la genera **MSBuild** en cada compilación, con los metadatos
  `StronglyTypedFileName` / `StronglyTypedClassName` del `EmbeddedResource`. Sale a `obj/`, no se
  versiona, y **una clave mal escrita es un error de compilación**.
- **Que no va al `.resx`**: el texto del aviso de protección de datos. Es contenido legal versionado
  (`PrivacyNotice.CurrentVersion`), no rótulo de interfaz; traducirlo sería un acto jurídico con su
  propia versión, no un cambio de cadena.
- **Añadir un idioma** será añadir `Textos.<cultura>.resx` al lado y la cultura en `Program.cs`, sin
  tocar ninguna vista.

---

## ADR-0020 — Interactividad global de Blazor, con las páginas públicas excluidas

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: medio, define como se sirve cada página

`arquitectura.md` describia interactividad **página a página**: SSR estático en lo público,
`InteractiveServer` en la gestión. Al montar la cáscara resultó que eso **no puede funcionar**: un
*layout* no hereda el modo de render de su página. Con `@rendermode` en la página, la página es
interactiva pero su cáscara no, y el botón que abre el cajón lateral queda pintado sin hacer nada.

- **Decisión**: interactividad **global** (`<Routes @rendermode="...">`), y las páginas que deben
  servirse estáticas lo dicen con `@attribute [ExcludeFromInteractiveRouting]`. `App.razor` consulta
  `HttpContext.AcceptsInteractiveRouting()` y pasa `null` como modo en esas peticiones.
- **Que se excluye y por que**:
  - *Registro, acceso y alta con Google*: entrar escribe la cookie de sesión, y eso necesita el
    `HttpContext` de una petición de verdad, que no existe dentro de un circuito.
  - *Portada, aviso de privacidad, error y página no encontrada*: para **no abrir un circuito de
    SignalR por cada visita anonima**. El nivel gratuito de Azure SignalR son unas 20 conexiones
    simultáneas en total; gastarlas en gente leyendo la portada dejaría sin sitio a los jugadores
    de un torneo en curso. La búsqueda pública del hito 5 se excluira por lo mismo.
- **Dos cáscaras**: `MainLayout` (pública, barra superior y poco más) y `StoreLayout` (gestión, con
  cajón lateral, dialogos y avisos emergentes). Están separadas porque la pública se pinta también
  sin interactividad, y ahí un cajón que se abre no tendría quien lo abriera.
- **Hay dos pruebas** que fijan esto: las páginas públicas no traen marca de isla interactiva en el
  HTML, y `/tienda` si.

---

## ADR-0021 — Los formularios de SSR estático llevan `name` y `Text` a mano, y la casilla es de Blazor

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, tres formularios

En SSR estático no hay circuito: el navegador envia el formulario y el servidor lo reconstruye a
partir del atributo `name` de cada campo. Los campos de MudBlazor no son los `InputText` de Blazor,
así que no lo ponen solos.

- **Campos de texto**: `MudTextField` con `name="Input.Email"` **y** `Text` con el valor. El `name`
  es por donde entra el dato; `Text` es lo único que hace que se dibuje el atributo `value`, y sin
  el un error de validación devolvería el formulario con todo borrado.
- **Casillas**: se usa el `InputCheckbox` de **Blazor**, no `MudCheckBox`. Una casilla HTML sin
  atributo `value` envia la cadena `"on"` al marcarse, y el enlace de formulario de Blazor no la
  acepta para un booleano (*"The value 'on' is not valid"*). MudBlazor no deja poner ese `value`
  porque el atributo choca con su propio parámetro `Value`. Importa porque esa casilla es la del
  consentimiento de protección de datos: si no llegase, el alta se rechazaría siempre.
- Las dos cosas están encapsuladas en `SsrTextField` y `SsrCheckBox`, y **fijadas con pruebas** que
  renderizan los componentes sin base de datos, para que una versión futura de MudBlazor que deje de
  reenviar el atributo rompa una prueba en vez de romper el registro en silencio.

---

## ADR-0022 — Las políticas nombradas se autentican solo con la cookie

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo · **Amplia**: ADR-0013

`CanOrganize`, `IsSuperadmin`, `IsPlayer` y la política por defecto aceptaban **cookie y Bearer**.
Al proteger la primera página se vio el problema: cuando se rechaza el acceso, ASP.NET lanza el
*challenge* de **todos** los esquemas de la política. El de la cookie escribe una redirección y el
de Bearer un 401, y la respuesta salía con **las dos cosas a la vez**, que no es nada que un
navegador sepa interpretar.

- **Decisión**: las políticas nombradas declaran solo `IdentityConstants.ApplicationScheme`. Los
  endpoints de la API ya declaraban Bearer explicitamente uno a uno (ADR-0013), así que la regla
  queda uniforme: **cada puerta acepta una sola llave**.
- Se configura además adonde manda la cookie a quien no pasa: `/cuenta/acceso` si le falta sesión
  (con el parámetro `volver`) y `/sin-permiso` si le falta permiso. Por defecto apuntaba a
  `/Account/Login`, que es de la plantilla de ASP.NET y aquí no existe.
- La página de acceso **solo acepta destinos dentro del sitio**: un enlace de phishing podría poner
  cualquier cosa en `volver`, y rebotar a otro dominio justo después de teclear la contraseña es
  precisamente lo que se busca al montar uno.

---

## ADR-0023 — La comprobación de tienda aprobada consulta en su propio ámbito

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, pero sienta precedente

`StoreDirectory` usaba el `TopCutDbContext` de la petición. La primera página de gestión reventaba
al abrirse: *"a second operation was started on this context instance"*. La política `CanOrganize`
la evaluan a la vez el endpoint y el `AuthorizeView` de la barra superior, Blazor los renderiza en
paralelo, y un `DbContext` **no admite dos operaciones simultáneas**.

- **Decisión**: `StoreDirectory` abre su propio ámbito de DI y su propio `DbContext` por consulta.
  Es una lectura única por índice, así que sale barato.
- **Alternativa descartada**: cachear la respuesta en la petición. No arregla la carrera (dos
  comprobaciones simultáneas empiezan antes de que ninguna cachee) y además pierde lo que da valor a
  ADR-0011: que revocar una tienda surta efecto en la petición siguiente.
- **Precedente para el hito 3b**: cualquier componente interactivo que consulte datos tiene el mismo
  problema. Si varios componentes de una pantalla van a leer a la vez, el acceso a datos tendrá que
  dejar de compartir el contexto de la petición (`IDbContextFactory` o un ámbito por operación).

---

## ADR-0024 — La tienda en contexto también se fija desde el circuito de Blazor

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: medio · **Amplia**: ADR-0011

`ITenantContext` sacaba la tienda del claim del usuario **leyendo el `HttpContext`**. Eso vale para
una petición HTTP, pero una página interactiva vive dentro de un circuito de SignalR y **ahí no hay
`HttpContext`**. Combinado con que el filtro de tenant falla cerrado, el resultado habría sido el
peor posible: la tienda abre su lista de torneos y la ve **vacía**, sin ningún error.

- **Decisión**: `TenantContext` mantiene el camino HTTP y añade `ITenantScope.Set(...)`.
  `StoreLayout` —la cáscara por la que pasan todas las pantallas de gestión— la fija una vez, leída
  del **mismo claim** que lee el camino HTTP. El layout se renderiza entero antes que la página que
  lleva dentro, así que el ámbito está puesto antes de la primera consulta.
- **Alternativa descartada**: leer `AuthenticationStateProvider` dentro del `ITenantContext`. Es
  asíncrono y el filtro de consulta de EF Core lo necesita sincrono; bloquear sobre una tarea dentro
  de un circuito es pedir un interbloqueo.
- **Alternativa descartada**: un `CircuitHandler`. Es el gancho del framework, pero el estado de
  autenticación no está garantizado al abrirse el circuito, y depender de ese orden es frágil.
- **Alternativa descartada**: pasar el id de tienda como parámetro en cada caso de uso. Explicito,
  pero renuncia al ámbito implicito que hace que **no se pueda olvidar** un filtro, que es el motivo
  entero del ADR-0011.
- **No amplia el acceso de nadie**: el valor sale del principal ya autenticado, las páginas están
  detras de `[Authorize(CanOrganize)]`, y si nadie lo fija el filtro sigue fallando cerrado.

---

## ADR-0025 — La hora que teclea la tienda se interpreta en una zona horaria configurada

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, una clase y una clave de configuración

El formulario de torneo pide fecha y hora; el dominio guarda un `DateTimeOffset`. Convertir con la
zona horaria **de la máquina** habría funcionado en el portátil (Madrid) y habría fallado en
producción: App Service corre en **UTC**, así que el mismo "18:00" tecleado en el mismo formulario
habría significado dos instantes distintos y el torneo habría empezado dos horas tarde.

- **Decisión**: `LocalTime` convierte en ambos sentidos usando una zona **de configuración**
  (`TopCut:TimeZone`), con `Europe/Madrid` por defecto, que es dónde está la tienda de la v1.
- **Alternativa descartada**: pedir la zona al navegador por JS. Es lo correcto cuando hay usuarios
  repartidos por el mundo; para una tienda local añade una llamada a JavaScript y un caso de fallo a
  cambio de nada.
- **Alternativa descartada**: guardar y mostrar todo en UTC. Obligaría al organizador a hacer la
  resta mentalmente cada vez que programa un torneo.
- Cuando haya tiendas en varias zonas, lo natural será que la zona sea un campo de la tienda en vez
  de configuración global. El cambio afecta a una clase.

---

## ADR-0026 — Lecturas y escrituras separadas: proyecciones para las listas, agregado para los cambios

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: medio, define como consultan las pantallas

El repositorio carga el agregado **entero** (participantes, rondas, emparejamientos y partidas)
porque emparejar y clasificar lo necesitan. Pintar una lista de treinta torneos con eso sería traer
media base de datos para mostrar una fecha y un contador.

- **Decisión**: dos caminos.
  - `ITournamentRepository` para **cambiar**: carga el agregado, aplica la operación y guarda **solo
    si sale bien**, todo en una unidad de trabajo con su propia conexión. Una regla rota a mitad no
    deja rastro, ni siquiera de lo que se aplicó antes de romperse.
  - `ITournamentQueries` e `ITcgCatalog` para **leer**: proyecciones sin seguimiento, con los
    contadores calculados por SQL Server.
- Ninguno de los dos usa `IgnoreQueryFilters`, así que el aislamiento entre tiendas sigue siendo del
  filtro y no de que alguien se acuerde de añadir un `Where`.
- **Guardar es atomico por pantalla**: la pantalla de edición manda datos y reglas en **una** sola
  llamada. Partido en cuatro, un torneo podría quedarse con las plazas nuevas y la duración de ronda
  vieja porque la tercera llamada fue rechazada.

---

## ADR-0027 — Las pantallas en vivo van por el circuito de Blazor, no por un hub propio

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: medio · **Cambia**: el plan del hito 4

El roadmap del hito 4 decía "hub de SignalR". Al montarlo se ve que **para la web no hace falta, y
además sería contraproducente**: toda pantalla de TopCut es una página de Blazor Server, y una
página de Blazor Server **ya tiene un WebSocket abierto** con el servidor. Añadirle un hub propio
significaría **dos conexiones por espectador**, y el nivel gratuito de App Service permite **cinco
en total**.

- **Decisión**: un difusor en memoria (`ITournamentBroadcaster` / `ITournamentWatch`). Quien guarda
  un cambio levanta su evento de dominio de siempre; un handler avisa al difusor; las pantallas que
  esten mirando ese torneo se enteran por **su propio circuito**, que es el que ya tenían abierto.
- **El mensaje no lleva datos**, solo "este torneo se ha movido". La pantalla vuelve a leer con la
  consulta que ya usa, así que hay **una sola descripción** de lo que enseña cada pantalla y quien
  se reconecta tras perderse un aviso acaba en el estado actual y no en un remiendo.
- **Primer suscriptor de los eventos de dominio.** Hasta ahora se levantaban y se tiraban a
  propósito (ADR-0010); esta es la prueba de que la costura era de verdad.
- **Se añade `RoundClosed`**, que faltaba: sin el, la pantalla de la sala seguía enseñando una ronda
  ya cerrada, con su reloj corriendo, hasta que alguien recargaba.
- **Que pasa con el hub**: sigue teniendo sentido, pero **para clientes externos** — la futura app
  móvil, que no tiene circuito. Construirlo ahora sería una superficie sin nadie que la consuma y
  sin forma de probarla de verdad, así que va con la Web API externa (hito 8) y la fase 2.
- **En producción** se delega en Azure SignalR Service, y eso vale igual: lo que se delega son los
  **circuitos de Blazor**, que es justo lo que resuelve el límite de cinco WebSockets.
- **Límite conocido**: el difusor es de un solo proceso. Con varias instancias de App Service cada
  una avisaría solo a sus propias pantallas. En el plan gratuito hay una sola instancia; el día que
  haya más, esto pasa a ser un backplane (Azure SignalR o Redis) detras de la misma interfaz.

---

## ADR-0028 — El cronómetro cuenta en el navegador desde un instante del servidor

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo

El servidor manda **el instante de fin en UTC y su propia hora**; el navegador calcula una vez la
diferencia entre los dos relojes y a partir de ahí lleva la cuenta atrás el solo.

- **Alternativa descartada**: un temporizador por circuito en el servidor. Serían un render y un
  diff por segundo **y por espectador**, y en el nivel gratuito de Azure SignalR (~20.000 mensajes
  al día) una sola pantalla abierta una tarde se comería la cuota entera.
- **Alternativa descartada**: mandar los segundos que quedan. Recargar la página reiniciaría el
  reloj, que es exactamente lo que `docs/arquitectura.md` dice que no puede pasar.
- **Por qué también se manda la hora del servidor**: un portátil con la hora mal puesta vería un
  tiempo equivocado si comparase el instante de fin con su propio reloj. Con la diferencia medida
  una vez, el cronómetro es correcto aunque el reloj del equipo no lo sea.
- El primer valor se pinta **desde el servidor**, así que la sala ve un tiempo antes de que el
  JavaScript tome el relevo, y la página sigue diciendo algo útil sin JavaScript.

---

## ADR-0029 — La búsqueda pública es el único sitio que cruza la frontera entre tiendas

**Fecha**: 2026-09-21 · **Estado**: aceptada · **Impacto**: alto

Hasta el hito 5 toda consulta pasaba por el filtro de tenant, que **falla cerrado** (ADR-0011): sin
tienda en contexto no se ve nada. La cara pública es justo lo contrario — un jugador anónimo tiene
que ver los torneos de **todas** las tiendas — así que en algún punto hay que apagar el filtro.

- **Decisión**: se apaga en **un solo sitio**, `PublicTournaments` (infraestructura), con
  `IgnoreQueryFilters()`. Es la única clase de la aplicación que lo hace a propósito.
- **Y se sustituye por una regla escrita a mano**, en un único método privado (`Visible`) del que
  parten los tres métodos públicos. Un filtro que vive en un sitio no se puede aplicar a medias:
  - el torneo está **publicado** — un borrador es el cuaderno de la tienda;
  - la tienda está **aprobada ahora mismo**, comprobado con un join y no con una copia del estado,
    para que revocar una tienda le quite los torneos de la lista en la petición siguiente;
  - y **no es una tienda de pruebas**, que es lo que hace de entorno de preproducción mientras el
    nivel gratuito de Azure SQL no dé para tener uno de verdad (`Store.IsTestStore`).
- **Un torneo cancelado sigue siendo público.** Esconderlo dejaría a quien se apuntó mirando un
  enlace roto la mañana del torneo; lo que se quita es el botón de inscribirse, no la información.
- **Los participantes llevan el mismo filtro**, así que la ficha necesita el mismo `IgnoreQueryFilters`
  para contar plazas. Sin él, los contadores saldrían a cero sin dar ningún error: exactamente el
  tipo de fallo silencioso contra el que el filtro existe. Hay una prueba solo para eso.
- **Alternativa descartada**: una vista o una tabla aparte con los torneos publicados. Duplicaría el
  dato y abriría la puerta a que la copia y el original no dijeran lo mismo.
- **Alternativa descartada**: resolver la tienda desde la dirección (`/tienda/{slug}/torneos`) y
  seguir usando el filtro. No sirve para buscar por ciudad, que es justo lo que pide el hito 5.
- **Escrituras**: esto es solo lectura. Inscribirse es una escritura contra el torneo de otra
  tienda, y eso se decide en el tramo 5b, aparte.

---

## ADR-0030 — Una página pública que no encuentra nada devuelve 404 y deja el cuerpo a `/not-found`

**Fecha**: 2026-09-21 · **Estado**: aceptada · **Impacto**: bajo

`/torneos/{id}` es una dirección pública: la va a seguir un buscador antes que una persona, así que
un torneo que no existe —o que su tienda no ha publicado— tiene que responder **404 de verdad**, no
un 200 con cara de disculpa.

- **Lo que se intentó primero**: poner el 404 y pintar un mensaje propio ("o no existe, o la tienda
  no lo ha publicado"). **No funciona**: en SSR estático, Blazor **descarta el cuerpo** de una
  página que cambia el código de estado. La respuesta salía con 404 y **cero bytes**.
- **Segundo intento**: apagar `IStatusCodePagesFeature` para que la tubería no reejecutase. Peor
  todavía: sin reejecución no había nada que enseñar, y quedaba la página en blanco.
- **Decisión**: poner el 404 y no pintar nada. `UseStatusCodePagesWithReExecute("/not-found")`
  reejecuta y devuelve la pantalla de "aquí no hay nada" que ya existe desde el hito 3a, con el 404
  puesto. Se pierde un mensaje más concreto y se gana que **todo el sitio conteste igual** a una
  dirección que no lleva a ningún sitio.
- **La prueba comprueba las dos cosas**: que el código es 404 y que el cuerpo **no está vacío**, que
  es el fallo que esto estuvo a punto de introducir.

---

## ADR-0031 — El jugador escribe en el torneo de otra tienda por una puerta con nombre

**Fecha**: 2026-09-21 · **Estado**: aceptada · **Impacto**: alto

El ADR-0029 abrió la lectura entre tiendas. Apuntarse es una **escritura** en el torneo de una
tienda a la que el jugador no pertenece, y eso no se puede colar por el camino normal.

- **Alternativa descartada**: fijar el ámbito de tienda al del torneo (`ITenantScope.Set`) durante
  la operación. Funciona, pero deja al resto de la petición actuando como esa tienda. Es
  exactamente el tipo de estado ambiental que un día se olvida de volver a su sitio.
- **Alternativa descartada**: un repositorio aparte para el jugador. Duplicaría la carga del
  agregado, sus `Include` ordenados y el manejo de choques de concurrencia.
- **Decisión**: un segundo método en el mismo repositorio, `ExecuteOnPublicAsync`, con el nombre
  diciendo lo que hace. Quita el filtro de tenant **solo para esa carga** y pone en su lugar la
  misma regla de visibilidad del ADR-0029 — el mismo método compartido, no una copia. De ahí en
  adelante todo es igual: el dominio decide, y `Tournament.Register` crea el participante con el
  `TenantId` del torneo, que es de donde tiene que salir.
- **Quién es el jugador** lo dice el principal autenticado, nunca el formulario. Un identificador
  de usuario en un campo oculto sería una invitación a apuntar a otro.
- **Un torneo que no es público responde "no existe"**, igual que para un buscador. Adivinar el
  identificador de un borrador no sirve de nada.
- **Darse de baja solo antes de empezar.** Con el torneo en marcha una baja cambia los
  emparejamientos y el organizador que está en la sala tiene que enterarse, así que a partir de ahí
  es cosa de la mesa de control y no de un botón en un móvil.

---

## ADR-0032 — La versión del torneo protege todo su agregado, no solo su fila

**Fecha**: 2026-09-21 · **Estado**: aceptada · **Impacto**: alto

Dos jugadores pulsando *Apuntarme* a la vez por la última plaza **no chocaban**. Cada uno insertaba
su fila en `Participants` y las dos escrituras entraban: la fila del torneo no cambiaba, así que no
había ninguna versión que la base de datos pudiera comparar. El torneo acababa con un jugador más
que plazas, sin error y sin rastro.

- **Decisión**: toda escritura que salga del repositorio marca también la **raíz** del agregado como
  modificada, de modo que su `RowVersion` entra en el `WHERE` del `UPDATE`. Para eso está una raíz
  de agregado: la consistencia se define sobre el conjunto, no sobre cada fila por separado.
- **Alternativa descartada**: una restricción en la base de datos. SQL Server no sabe expresar
  "menos filas hijas que el valor de esta columna" de forma declarativa.
- **Alternativa descartada**: una transacción serializable alrededor de la inscripción. Bloquea de
  más para lo que es un choque raro, y el nivel gratuito de Azure SQL no está para regalar bloqueos.
- **Efecto secundario que interesa**: `UpdatedAt` del torneo pasa a significar de verdad "la última
  vez que se tocó algo de este torneo", y no solo "la última vez que se editó su configuración".
- **Coste**: cada operación escribe también la fila del torneo. Una fila estrecha, una vez por
  operación de organizador, contra vender dos veces la misma plaza.
- **La prueba** monta la carrera de verdad contra SQL Server: Bruno carga, se apunta y guarda
  mientras Ana todavía tiene el agregado en la mano. Ana recibe el mensaje de recargar, y el torneo
  acaba con exactamente las plazas que tiene.

---

## ADR-0033 — La pantalla del jugador no abre circuito: se refresca sola

**Fecha**: 2026-09-21 · **Estado**: aceptada · **Impacto**: alto

La mesa de control y la pantalla de la sala son páginas interactivas de Blazor, y cada una gasta un
WebSocket. El plan gratuito de App Service permite **cinco en total** (ADR-0027). Un torneo de
dieciséis personas con el móvil abierto pediría dieciséis más.

- **Decisión**: la pantalla del jugador va en **SSR estático**. Es la única de las tres pantallas en
  vivo que se multiplica por el número de jugadores, así que es la única que no puede permitirse un
  circuito.
- **El reloj**, en consecuencia, no se arranca por interoperabilidad desde el servidor sino desde
  una etiqueta `<script type="module">` de la propia página, leyendo los mismos atributos `data-`
  que ya pinta el componente. El JavaScript es exactamente el mismo del ADR-0028; lo único que
  cambia es quién lo pone en marcha. Sigue sin costar un mensaje por segundo y por jugador.
- **Y lo que sustituye al circuito** es un `<meta http-equiv="refresh">` de 25 segundos, puesto
  **solo cuando no hay nada que pulsar**: esperando emparejamiento, o con el resultado ya dado. En
  cuanto aparecen los botones deja de refrescarse, porque recargar la página debajo del pulgar de
  alguien es peor que esperar. Un GET pequeño cada 25 segundos es mucho más barato que mantener
  abierto un WebSocket, y funciona en cualquier teléfono.
- **Alternativa descartada**: hacerla interactiva y confiar en que no vengan tantos jugadores. Es
  apostar a que la aplicación no se use.
- **Alternativa descartada**: un hub de SignalR aparte para los móviles. Mismo problema de
  conexiones, más superficie, y ya está decidido que el hub va con la Web API externa (hito 8).

---

## ADR-0034 — El resultado se reporta una vez, y desde el lado de quien lo reporta

**Fecha**: 2026-09-21 · **Estado**: aceptada · **Impacto**: medio

Dos decisiones sobre cómo un jugador reporta su propia partida.

**Los botones se leen desde el jugador, no desde la mesa.** La mesa de control lee todos los
enfrentamientos desde el lado del jugador A, porque está mirando una lista. Quien está sentado a una
mesa no: sabe quién es, y el botón que busca es el que dice lo que le ha pasado **a él**. Así que en
su pantalla pone "Gano 2-1", no "1-2". La vuelta a la representación del torneo —que guarda siempre
desde el lado de A— se hace en un solo sitio (`PlayerOutcome`), y el orden de los botones es el
mismo para los dos jugadores: leerlos en espejo es un toque equivocado esperando a pasar.

**Una vez reportado, no se cambia desde el móvil.** El primero que reporta cierra la partida; si el
otro no está de acuerdo, se habla con la tienda, que ya tenía desde el hito 3d el botón de corregir
y deja marcado que lo hizo.

- **Alternativa descartada**: que el rival confirme. Duplica los toques y deja partidas a medias
  cuando alguien se va sin confirmar, que es exactamente lo que pasa en una tienda.
- **Alternativa descartada**: que cualquiera de los dos pueda sobrescribir. Dos teléfonos
  corrigiéndose es como una mesa acaba con un resultado que no habían acordado ninguno de los dos.
- Es además como funciona un acta de papel firmada, que es el procedimiento que esto sustituye.
- **La mesa que se reporta se comprueba en el servidor**, no solo al pintar la pantalla: el
  identificador del emparejamiento viaja en un formulario, así que alguien podría enviar el de otra
  mesa.

---

## ADR-0035 — Quién es la misma persona entre torneos lo decide la tienda, no el nombre

**Fecha**: 2026-09-22 · **Estado**: aceptada · **Impacto**: alto · **Cambia**: el esquema

Una liga tiene que sumar lo que alguien ha puntuado en varios torneos, y hasta ahora **no había a
qué sumarlo**: un participante es un nombre tecleado en el mostrador, y las dos "Ana" de dos
torneos pueden ser la misma persona o no.

- **Alternativa descartada**: puntuar solo a quien tenga cuenta. Cero cambios de esquema, pero en
  una tienda donde la mitad de los habituales no se registra la tabla sale coja.
- **Alternativa descartada**: agrupar por nombre normalizado. Tampoco toca el esquema, pero funde a
  dos personas que se llaman igual y parte en dos a quien se apuntó con dos grafías, **sin sitio
  donde arreglarlo**.
- **Decisión** (elegida por Jaime): una entidad nueva, `StorePlayer`, el **fichero de jugadores de
  la tienda**. Cada `Participant` apunta a una ficha, y la ficha es lo que la liga suma.

Lo que se gana es que la identidad pasa a ser un dato que alguien puede corregir, en vez de una
suposición del código. Lo que cuesta es una migración y una entidad más.

### Las dos puertas, y por qué no son la misma

- **El mostrador**: lo que la tienda teclea se busca en **su propio fichero**. Escribir "Ana Pérez"
  en Kraken Cards significa la Ana Pérez de Kraken Cards, que es lo que ha hecho siempre un libro de
  papel. Se sigue pudiendo escribir e Intro sin tocar el ratón; lo nuevo es que la pantalla enseña a
  quién va a apuntar antes de hacerlo.
- **La inscripción por web**: solo empareja por **cuenta**, jamás por nombre. Aquí no hay nadie en la
  sala para darse cuenta de que la ha fundido con quien no era.
- Consecuencia buscada: alguien que jugó de mostrador y luego se registra acaba con **dos fichas**.
  La pantalla del fichero las marca como posible duplicado y la tienda las fusiona (tramo 6b). Eso
  es preferible a fusionarlas solas y que nadie se entere.

### Detalles

- **`NormalizedName`** es una columna, no un cálculo: sin tildes, sin mayúsculas y sin espacios de
  más. Lo justo para que "Ana Pérez" y "ana perez " no sean dos, y deliberadamente nada más listo,
  porque cualquier cosa más difusa empezaría a fundir a gente distinta.
- **Sin índice único por nombre**, a propósito: una tienda puede tener dos Anas de verdad. El botón
  *"Es otra persona"* del mostrador es el que lo dice, y por eso teclear el mismo nombre dos veces
  responde "ya está inscrito" en vez de crear un duplicado en silencio.
- **Índice único por `(TenantId, UserId)`**, ese sí: la cuenta es el vínculo que no puede
  equivocarse, y dos fichas para una cuenta partirían sus puntos por la mitad.
- **El nombre del participante sigue siendo una foto** del momento en que se apuntó. Corregir el
  fichero arregla el fichero, no los emparejamientos del mes pasado.
- **La migración rellena el fichero** de los torneos que ya existían, con la misma regla: una ficha
  por cuenta y una por nombre entre los de mostrador. La normalización en SQL se hace con la
  conversión a una intercalación sin acentos, que es la única forma de quitarlos en T-SQL y sí trata
  bien la eñe; verificada contra las mismas cadenas que prueban el C#.
