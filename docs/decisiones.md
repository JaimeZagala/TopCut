# Registro de decisiones (ADR)

Cada decision de diseno no trivial se anota aqui con fecha, la decision tomada, y la alternativa
descartada y por que. Si una decision tomada en el chat contradice algo escrito en `/docs`, se
actualiza el documento afectado y se anade una entrada nueva aqui.

---

## ADR-0001 — Formato de archivo de solucion: `.slnx` en vez de `.sln`

**Fecha**: 2026-09-19 · **Estado**: aceptada · **Impacto**: bajo, reversible en un comando

El brief pedia `TopCut.sln`. `dotnet new sln` en el SDK 10 genera por defecto el formato **`.slnx`**
(XML, legible, sin GUIDs duplicados, mucho menos ruidoso en los diffs). Se conserva el default:
**`TopCut.slnx`**.

- **Alternativa descartada**: forzar `.sln` clasico con `dotnet new sln --format sln`.
- **Por que**: `.slnx` esta soportado por la CLI de .NET 10 y por Visual Studio 2022 17.14+ / VS 18
  (el equipo tiene VS 18.10). El formato clasico solo aportaria compatibilidad con herramientas
  antiguas que aqui no se usan.
- **Como revertir**: `dotnet sln migrate` en sentido contrario, o regenerar con `--format sln` y
  volver a anadir los proyectos. Cinco minutos.

---

## ADR-0002 — Propiedades y versiones de paquetes centralizadas

**Fecha**: 2026-09-19 · **Estado**: aceptada · **Impacto**: bajo, reversible

Se anaden `Directory.Build.props` (TargetFramework, nullable, implicit usings, `LangVersion`,
`InvariantGlobalization=false`) y `Directory.Packages.props` (Central Package Management: los
`.csproj` declaran `PackageReference` **sin** `Version`).

- **Alternativa descartada**: repetir las propiedades y versiones en cada `.csproj`.
- **Por que**: con 7 proyectos y creciendo, una sola version por paquete evita desalineaciones
  silenciosas entre capas, que es una fuente clasica de errores de runtime con EF Core y ASP.NET.
  Es mas barato adoptarlo ahora (3 proyectos con paquetes) que mas tarde.

---

## ADR-0003 — La plantilla de Blazor se genero sin el scaffolding de Identity (`--auth None`)

**Fecha**: 2026-09-19 · **Estado**: aceptada · **Impacto**: medio, reversible con trabajo manual

`dotnet new blazor -au Individual` habria generado un `ApplicationDbContext` y las paginas de
Identity **dentro de `TopCut.Web`**, lo que rompe la regla de capas (EF Core e Identity pertenecen
a `TopCut.Infrastructure`). Se genero con `-au None` y la identidad se cableara a mano en el
hito 1, con el `DbContext` en Infrastructure.

- **Alternativa descartada**: usar el scaffolding y luego mover los archivos de capa.
- **Por que**: mover el scaffolding despues arrastra rutas, namespaces y `_Imports` a medio migrar;
  cablearlo limpio desde el principio sale mas barato y deja la capa correcta desde el commit 1.

---

## ADR-0004 — `TopCut.Api` es una biblioteca alojada por `TopCut.Web` (proceso unico)

**Fecha**: 2026-09-19 · **Estado**: aceptada (confirmada por Jaime) · **Impacto**: medio

`TopCut.Api` es una **biblioteca de clases** con los controladores REST y los hubs de SignalR.
`TopCut.Web` la referencia y la registra (`AddControllers().AddApplicationPart(...)`), de modo que
la UI Blazor y los endpoints `/api/*` se sirven desde **un unico proceso y un unico despliegue**.

- **Alternativas descartadas**:
  - *Dos App Services separados*: dos cold starts (y en el F1 gratuito el cold start ya duele),
    dos configuraciones de Identity, y CORS que gestionar. El escalado independiente que aportaria
    es irrelevante al volumen previsto (una tienda, 12-32 jugadores por torneo).
  - *Controladores dentro de `TopCut.Web`, sin proyecto `TopCut.Api`*: mas simple, pero pierde la
    separacion de namespaces que describe el brief y facilita que se cuele logica de negocio en
    los controladores.
- **Por que**: se conserva la frontera explicita entre "UI" y "API para clientes externos" a nivel
  de proyecto, sin pagar el coste operativo de dos despliegues. Si algun dia hace falta separarlos,
  basta con anadir un host `TopCut.Api.Host` de 20 lineas que referencie la misma biblioteca.

---

## ADR-0005 — Herramientas del equipo de desarrollo

**Fecha**: 2026-09-19 · **Estado**: aceptada y ejecutada

Estado inicial del equipo: .NET SDK 10.0.401, SQL Server LocalDB (`MSSQLLocalDB`) y el workload
`maui-windows`; **sin git**, y con Docker Desktop **instalado pero roto**.

Decision (confirmada por Jaime): instalar **git y Docker Desktop**.

- **git 2.55.0.3**: instalado via `winget` en `C:\Program Files\Git`. Desbloquea el control de
  versiones, el repositorio en GitHub y el CI/CD del hito 8.
- **Docker Desktop 29.8.0**: habia una instalacion obsoleta y rota (v26.0.0, con el daemon caido y
  sin su distro WSL). Jaime la reinstalo; el daemon corre con **contenedores Linux**, que es lo que
  Testcontainers necesita.
- **GitHub CLI 2.101.0**: instalado para crear y gestionar el repositorio remoto.
- **Alternativa descartada**: pruebas de integracion contra LocalDB en local y Testcontainers solo
  en CI. Descartada porque LocalDB y Azure SQL difieren en restricciones y collation, y el objetivo
  de estas pruebas es precisamente validar el esquema contra un SQL Server real antes del PR.

Nota operativa: `git`, `docker` y `gh` se instalaron durante una sesion ya abierta, asi que sus
rutas (`C:\Program Files\Git\cmd`, `C:\Program Files\Docker\Docker\resources\bin`,
`C:\Program Files\GitHub CLI`) no estan en el `PATH` de terminales anteriores a la instalacion.
Abrir una terminal nueva lo resuelve.

---

## ADR-0006 — Claves primarias: `int` para catalogos, `Guid` v7 para entidades de negocio

**Fecha**: 2026-09-19 · **Estado**: aceptada (confirmada por Jaime) · **Impacto**: alto, dificil de revertir

- **`int` autoincremental** en catalogos pequenos, estables y no expuestos en URLs publicas:
  `Tcg`, `Ruleset` y tablas de apoyo similares.
- **`Guid` secuencial v7** (`Guid.CreateVersion7()`, disponible desde .NET 9) en las entidades de
  negocio que aparecen en URLs o se sincronizan con clientes: `Tienda`, `Torneo`, `Participante`,
  `Ronda`, `Emparejamiento`, `Partida`, `Liga`.

- **Alternativas descartadas**:
  - *`int` en todo*: indices mas compactos y URLs cortas, pero `/torneo/42` revela cuantos torneos
    lleva la plataforma — mal detalle en un producto que aspira a abrirse a otras tiendas — y
    complica fusionar datos si algun dia se importa desde otra herramienta.
  - *`Guid` en todo*: uniforme, pero infla innecesariamente los indices de catalogos que nunca
    pasaran de una docena de filas.
- **Por que v7 y no v4**: los GUID v7 son **monotonos en el tiempo**, asi que se comportan como
  claves agrupadas secuenciales en SQL Server y no fragmentan el indice como haria un v4 aleatorio.
- **Nota para el hito 1**: los IDs de ASP.NET Core Identity (`AspNetUsers`, `AspNetRoles`) quedan
  como `string` — es el default del framework y cambiarlo aporta poco.

---

## ADR-0007 — Identidad en los commits y correo para los servicios del proyecto

**Fecha**: 2026-09-19 · **Estado**: aceptada

### Repositorio e identidad en los commits — decidido

El repositorio vive en **`JaimeZagala/TopCut`**, **privado**, bajo la cuenta personal de GitHub de
Jaime. Los commits se firman con la direccion `noreply` que GitHub proporciona:
`692421+JaimeZagala@users.noreply.github.com`.

- **Alternativas descartadas**:
  - *Repositorio publico desde el principio*: el repo acabara conteniendo configuracion de Azure y
    nombres de tiendas y jugadores reales. Privado → publico es un clic; al reves no, porque lo
    indexado ya esta fuera.
  - *Firmar con el Gmail personal*: en un repo que puede volverse publico, el correo queda en cada
    commit para siempre y es rastreable por bots.
  - *Una segunda cuenta de GitHub para el proyecto*: friccion permanente (dos sesiones, dos claves,
    dos 2FA) y el codigo dejaria de contar como trabajo propio de Jaime.
- **Ejecucion**: los dos commits que ya existian se reescribieron con `git commit-tree` antes del
  primer push, de modo que **el Gmail personal nunca llego a GitHub**.

### Correo propio para los servicios — decidido

Se creo la cuenta **`topcut.tcgapp@gmail.com`**, que es la identidad de **Azure**, **Google Cloud**
(el OAuth del login con Google) y, en la fase 3, **Google Play Console**.

- **Por que separarlo**: la cuenta de desarrollador de Play esta atada a la cuenta que la crea y
  transferirla despues es un tramite de soporte lento e incierto. Ademas, la oferta gratuita de
  Azure SQL es **una base de datos por suscripcion**: una cuenta nueva estrena su propio cupo.
- **Por que hacerlo antes del hito 1**: el login con Google necesita un proyecto de Google Cloud, y
  crearlo bajo la cuenta equivocada obliga a rehacerlo.
- **Riesgo a mitigar**: se convierte en un punto unico de fallo (Azure + Google Cloud + Play). Exige
  2FA y correo de recuperacion apuntando a la cuenta personal.
- **Lo que NO cambia**: GitHub sigue siendo la cuenta personal. El correo del proyecto se anade como
  email secundario verificado en esa misma cuenta si se quiere enrutar notificaciones.

---

## ADR-0008 — Codigo en ingles, textos visibles y documentacion en espanol

**Fecha**: 2026-09-19 · **Estado**: aceptada (confirmada por Jaime) · **Impacto**: alto si se revierte tarde

Los identificadores y los comentarios del codigo van en **ingles** (`Tournament`, `Participant`,
`Pairing`, `Round`, `Ruleset`, `Store`). Se quedan en **espanol**:

- `/docs` al completo, que es lo que lee Jaime.
- Los **mensajes de `DomainException`**, porque pueden acabar en pantalla y el producto es
  monolingue en espanol. Pasaran a recursos `.resx` en el hito 3, y entonces el codigo quedara
  limpio de cadenas traducibles.

- **Primera version de este ADR** (descartada el mismo dia): dominio en espanol para que el
  vocabulario del negocio, la documentacion y el codigo usaran el mismo termino.
- **Por que se cambio**: convencion mas habitual y legible para cualquiera de fuera que mire el
  repositorio; encaja sin roces con el vocabulario de ASP.NET y EF Core; y evita identificadores
  con tildes o enes.
- **Coste real del cambio**: se ejecuto en el mismo hito 1a, sobre 19 archivos de dominio y 3 de
  pruebas. Hacerlo tres hitos mas tarde habrian sido cientos.

## ADR-0009 — Fin de tiempo de ronda: politica configurable, por defecto la oficial

**Fecha**: 2026-09-19 · **Estado**: aceptada (confirmada por Jaime)

El brief pedia que, al llegar el cronometro a cero sin resultado reportado, el enfrentamiento se
marcase **automaticamente como empate**. Al validar los reglamentos oficiales aparecio que **ambos
juegos dicen otra cosa**:

- **Magic** (MTR): tras los cinco turnos extra, gana el enfrentamiento **quien vaya por delante en
  partidas**; solo hay empate si van igualados.
- **One Piece** (TRM 5.2): tres turnos extra con limite de 5 minutos y, si sigue sin haber ganador,
  se resuelve por criterios de juego antes de declarar empate.

Con la regla literal del brief, un 1-0 en Bo3 al que se le acaba el tiempo se registraria como
empate cuando por reglamento es victoria.

**Decision**: el ruleset lleva una politica `TimeExpiryPolicy` con dos valores, y la tienda puede
cambiarla por torneo hasta que empiece:

| Valor | Comportamiento |
|---|---|
| `LeaderWins` (**por defecto en ambos TCG**) | Gana quien va por delante en partidas; empate solo si van igualados. Es lo que prescriben los dos reglamentos. |
| `AlwaysDraw` | Empate siempre, pase lo que pase. Es la regla literal del brief, disponible para quien prefiera cerrar rondas rapido y corregir a mano. |

**Consecuencia de diseno que esto obligo a resolver**: para que `LeaderWins` signifique algo hay
que saber quien iba por delante, y eso exige poder **registrar cada partida segun termina**, no
solo el resultado completo al final. De ahi `Tournament.RecordGame`, que anade una partida y cierra
el enfrentamiento solo en cuanto queda decidido. Encaja ademas con el requisito de que el jugador
pueda reportar en un par de toques desde el movil.

En los dos casos el organizador puede corregir el resultado despues, y el enfrentamiento queda
marcado con `DecidedByTimeExpiry` para que la pantalla en vivo pueda destacarlo.

---

## ADR-0010 — Despacho de eventos de dominio sin libreria de mediador

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, reversible

El brief pedia publicar los eventos de dominio "mediante un mediador tipo MediatR". Se implementa
con un despachador propio de unas treinta lineas: `IDomainEventHandler<TEvent>` e
`IDomainEventDispatcher` en Application, y una implementacion en Infrastructure que resuelve los
handlers registrados en el contenedor de DI. Un interceptor de EF Core recoge los eventos de las
entidades rastreadas y los publica **despues** de que el guardado haya tenido exito.

- **Alternativa descartada**: MediatR.
- **Por que**: hay cinco tipos de evento, ningun suscriptor en la v1 y ninguna necesidad de
  pipeline de peticion/respuesta. El despachador propio cubre exactamente eso y deja el proyecto sin
  una dependencia mas que mantener.
- **Sobre la licencia**: MediatR 13 pasó a licencia comercial en julio de 2025, pero mantiene un
  tier Community gratuito por debajo de 5 M$ de facturacion anual, asi que **para este proyecto
  seria gratuito**. La decision es de simplicidad, no de coste.
- **Como revertir**: si mas adelante la capa Application quiere un pipeline de comandos con
  validacion y logging transversales, cambiar a MediatR es sustituir la implementacion del
  despachador; los handlers se renombran a `INotificationHandler` y poco mas.

---

## ADR-0011 — El filtro de tenant falla cerrado

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: alto en seguridad, barato de mantener

El filtro global de EF Core es `entity.TenantId == context.CurrentTenantId`. Cuando no hay tienda en
contexto, la comparacion es contra `NULL` y **no devuelve ninguna fila**.

- **Alternativa descartada**: que un contexto sin tenant vea todo, que es lo comodo para la busqueda
  publica de torneos y para el panel de Superadmin.
- **Por que**: con esa variante, cualquier fallo que deje el tenant sin resolver —un endpoint nuevo
  que olvide el middleware, un job en segundo plano, un test mal montado— expondria los datos de
  todas las tiendas en silencio. Con el filtro actual el mismo fallo produce una lista vacia: se ve
  enseguida y no filtra nada.
- **Consecuencia**: leer entre tiendas exige `IgnoreQueryFilters()` explicito en el sitio de la
  consulta. La busqueda publica y el panel de Superadmin lo usaran, y al ser explicito queda
  visible en revision de codigo en vez de ser el comportamiento por defecto.
- `Store` no lleva filtro: una tienda **es** el tenant, no pertenece a uno.

---

## ADR-0012 — Las pruebas de integracion aceptan un SQL Server externo

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo

Por defecto la bateria levanta SQL Server con Testcontainers, como fija el ADR-0005. Si la variable
de entorno `TOPCUT_TEST_SQL` trae una cadena de conexion, se usa esa instancia y se recrea el
esquema desde cero al arrancar.

- **Por que**: desbloquea el desarrollo cuando Docker no esta disponible en la maquina (LocalDB
  sirve de sustituto), y permite que un runner de CI que ya ofrece SQL Server como service
  container lo reutilice en vez de anidar un contenedor dentro de otro.
- **Lo que no cambia**: el camino por defecto sigue siendo Testcontainers, y es el que debe
  ejecutarse antes de fusionar un cambio de esquema. LocalDB y Azure SQL no son identicos.

---

## ADR-0013 — Los endpoints de la API se autentican solo con Bearer

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: seguridad

Las politicas con nombre (`IsSuperadmin`, `CanOrganize`, `IsPlayer`) aceptan **cookie y Bearer**,
porque las paginas Blazor las necesitan. Los endpoints bajo `/api/v1/*`, en cambio, exigen
**exclusivamente Bearer**.

- **Por que**: si un endpoint de la API aceptase la cookie de sesion, un formulario alojado en otro
  sitio podria hacer que el navegador de la victima enviase la peticion con su cookie adjunta, que
  es exactamente un CSRF. Con Bearer no hay riesgo: el token va en una cabecera que solo puede
  poner el cliente, nunca el navegador por su cuenta.
- **Coste**: ninguno. La UI Blazor **no llama a la API**: invoca los mismos servicios de
  `TopCut.Application` en el mismo proceso (ADR-0004). La API existe para clientes externos, que
  usan Bearer de todas formas.
- Los grupos de la API llevan ademas `DisableAntiforgery()`: son JSON y el middleware de
  antiforgery solo tiene sentido para formularios con cookie.

---

## ADR-0014 — Las paginas de error amables no se aplican a `/api`

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, pero corrige un fallo real

`UseStatusCodePagesWithReExecute` reejecuta la tuberia contra una pagina Razor para cualquier
respuesta de error sin cuerpo. Aplicado a toda la aplicacion convertia un **401 de la API en un 400
con HTML**, inutil para un cliente que espera `problem+json`. Ahora va envuelto en un `UseWhen` que
excluye las rutas `/api`.

Lo detectaron las pruebas de integracion del hito 1c, no una revision del codigo: es el tipo de
fallo que solo aparece cuando algo llama de verdad al endpoint y mira el codigo de estado.

---

## ADR-0015 — La base de datos de desarrollo es SQL Server en Docker, no LocalDB

**Fecha**: 2026-09-20 · **Estado**: aceptada (confirmada por Jaime) · **Impacto**: bajo, solo afecta al puesto de desarrollo

El desarrollo local usa el SQL Server 2022 que levanta el [`compose.yaml`](../compose.yaml) de la
raiz, en el puerto **11433** y con los datos en un volumen con nombre. `appsettings.Development.json`
y el `DesignTimeDbContextFactory` apuntan ahi.

- **Alternativa descartada**: SQL Server LocalDB, que era la eleccion inicial del ADR-0005.
- **Por que se cambio**:
  - **Es otro motor**. LocalDB es una variante de escritorio; produccion es Azure SQL y las pruebas
    de integracion ya usan el SQL Server de un contenedor. Con esto los tres entornos coinciden.
  - **Se apaga solo** cuando lleva unos minutos sin uso, y despertar tarda lo justo para perder la
    carrera contra el arranque de la aplicacion. Eso es lo que obligo a escribir la espera del
    `DatabaseInitializer` — que se queda igualmente, porque **Azure SQL serverless tambien se
    pausa** y el problema es real en produccion.
  - **Su estado vive en el registro de Windows** (`HKCU\SOFTWARE\Microsoft\Microsoft SQL Server\UserInstances`),
    separado de los ficheros en disco. Cuando las dos mitades se desincronizan, los errores que da
    apuntan a cualquier sitio menos a la causa: "no puedo crear el fichero porque ya existe"
    seguido de "la base ya existe" seguido de que la instancia entera deja de arrancar.
  - Un contenedor **se tira y se vuelve a levantar** (`docker compose down -v`) sin dejar residuos
    en el perfil del usuario ni en el registro.
- **Coste**: Docker Desktop pasa de ser opcional a requisito para desarrollar. Ya lo era en la
  practica: sin el no se pueden ejecutar las pruebas de integracion antes de fusionar un cambio de
  esquema (ADR-0005).
- **Lo que no cambia**: las pruebas de integracion siguen usando Testcontainers, con su propio
  contenedor efimero. La base de desarrollo y la de las pruebas no se tocan entre si.

---

## ADR-0016 — Emparejamiento suizo por backtracking con preferencia Dutch

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: alto en correccion, contenido en una clase

El emparejador ordena a los jugadores por clasificacion y los empareja recorriendo la lista con
**backtracking**. El orden en que prueba a los candidatos es lo que lo hace Dutch: el rival
preferido del primer jugador es el que le corresponde en la otra mitad de su grupo de puntuacion, y
a partir de ahi prueba hacia los lados.

- **Alternativa descartada**: emparejar de una pasada, dividiendo cada grupo en dos mitades y
  cruzandolas directamente. Es lo que describe el metodo Dutch, y es mas simple.
- **Por que no**: esa pasada unica falla cuando el cruce que toca ya se jugo. Entonces hay que
  improvisar intercambios, y con grupos impares que descuelgan jugadores al grupo siguiente el
  parcheo se vuelve un nido de casos. El backtracking **garantiza encontrar un emparejamiento sin
  repeticiones siempre que exista alguno**, y como explora en orden de preferencia Dutch, el
  resultado coincide con la pasada simple cuando esta funcionaria.
- **Repeticiones**: se hace una primera pasada que las prohibe por completo. Solo si esa pasada
  agota todas las combinaciones sin exito se repite permitiendolas. Eso es literalmente "evitar
  repetir mientras existan alternativas": con 4 jugadores y 4 rondas no hay alternativa posible, y
  el torneo tiene que poder continuar igualmente.
- **Coste medido**: el backtracking es exponencial en el peor caso teorico, pero en la practica la
  primera combinacion que prueba suele valer, porque la preferencia Dutch es justamente la que casi
  siempre funciona. Medido sobre campos de 4 a 128 jugadores con las rondas que recomienda el
  apendice E del MTR:

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

  Lo que encarece la busqueda **no es el tamano del campo sino cuantas parejas quedan libres**: 37
  jugadores tardan cuatro veces mas que 128 porque con un campo impar mediano y seis rondas las
  combinaciones disponibles escasean y hay que retroceder de verdad. Aun asi son milisegundos.

  El caso limite es el round robin completo, donde solo existe una familia de horarios validos:
  10 jugadores en 9 rondas, o 7 jugadores en 7 rondas con un bye cada uno, se resuelven en menos de
  1 ms y sin una sola repeticion.

---

## ADR-0017 — El enfrentamiento directo solo desempata a dos jugadores aislados

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, pero es una limitacion consciente

El criterio `HeadToHead` solo se aplica cuando quedan **exactamente dos** jugadores igualados en
todo lo anterior y no hay nadie mas con sus mismos puntos. En grupos de tres o mas, no se aplica.

- **Por que**: el enfrentamiento directo **puede ser circular**. Si A gano a B, B gano a C y C gano
  a A, no existe ningun orden coherente que respete los tres resultados, y cualquier algoritmo que
  lo intente acaba dependiendo del orden en que mire las parejas.
- **Que dice el reglamento**: el manual de One Piece envia esos casos a **jugar otra partida** y, si
  no hay tiempo, a un metodo aleatorio decidido por el juez. Es decir, reconoce que el algoritmo se
  acaba ahi.
- **Consecuencia practica**: un triple empate exacto queda en el orden que dejaron los criterios
  numericos, y el organizador decide. Es lo correcto, no una carencia: fabricar un desempate donde
  el reglamento dice que no lo hay seria peor que no fabricarlo.

---

## ADR-0018 — La tabla de rondas por asistencia tambien es dato del ruleset

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, una columna y una migracion

Cuantas rondas suizas jugar y de que tamano hacer el top cut segun la asistencia **no es una
formula universal**: cada TCG publica su propia tabla, y no coinciden.

| Asistencia | Magic (MTR apendice E) | One Piece (TRM 3.6) |
|---|---|---|
| 4-8 | 3 rondas, sin cut | 3 rondas, sin cut |
| 9-16 | 5 rondas, **Top 4** | **4 rondas**, **Top 2** |
| 17-32 | 5 rondas, **Top 8** | 5 rondas, **Top 4** |
| 33-64 | 6 rondas, Top 8 | 6 rondas, Top 8 |
| 65-128 | 7 rondas, Top 8 | 7 rondas, Top 8 |
| 129-226 / 129-256 | 8 rondas, **Top 8** | 8 rondas, **Top 16** |
| 227-409 / 257-512 | 9 rondas, Top 8 | 9 rondas, Top 16 |
| 410+ / 513-1024 | 10 rondas, Top 8 | 10 rondas, **Top 32** |

- **Alternativa descartada**: una funcion compartida `RecommendedRounds(jugadores)`. Se acerca
  bastante a `techo(log2(n))`, que es el principio del sistema suizo, pero **se equivoca en las
  bandas medias y en el tamano del cut**, que es justo donde se juega la mayoria de los torneos de
  tienda. Con 12 jugadores propondria lo mismo a los dos juegos, y los dos reglamentos piden cosas
  distintas.
- **Decision**: `Ruleset.AttendanceBands`, una lista ordenada de bandas con su limite superior, sus
  rondas y su tamano de cut opcional, almacenada en una columna compacta igual que los criterios de
  desempate. `RecommendationFor(jugadores)` devuelve la banda que toca.
- **Es una sugerencia, no una imposicion**: la tienda puede poner las rondas y el cut que quiera. El
  valor esta en que la pantalla de crear torneo llegue con el numero correcto ya puesto, porque
  elegir tres rondas para 27 jugadores deja varios invictos y decide el torneo por desempate en vez
  de por partidas.
- **Migracion**: el seed rellena la tabla en los rulesets que ya existan y esten vacios, pero nunca
  pisa una que el Superadmin haya ajustado.

---

## ADR-0019 — Los textos de interfaz salen de un `.resx` con clase generada en la compilacion

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, afecta a las vistas

El brief pide que los textos visibles vivan en archivos de recursos desde el principio aunque la v1
sea solo en espanol. Se usa **`Resources/Textos.resx`** y se accede como `@Textos.HomeTitle`.

- **Alternativa descartada**: `IStringLocalizer<T>` con claves en cadena (`L["HomeTitle"]`), que es
  lo idiomatico en ASP.NET Core. Se descarta porque una clave mal escrita no falla: pinta la propia
  clave en pantalla y nadie se entera hasta que lo ve un usuario.
- **Alternativa descartada**: `.Designer.cs` generado por Visual Studio y versionado. Se descarta
  porque solo se regenera desde el IDE, y este proyecto se edita tambien desde la linea de comandos:
  el archivo acabaria desincronizado del `.resx`.
- **Decision**: la clase la genera **MSBuild** en cada compilacion, con los metadatos
  `StronglyTypedFileName` / `StronglyTypedClassName` del `EmbeddedResource`. Sale a `obj/`, no se
  versiona, y **una clave mal escrita es un error de compilacion**.
- **Que no va al `.resx`**: el texto del aviso de proteccion de datos. Es contenido legal versionado
  (`PrivacyNotice.CurrentVersion`), no rotulo de interfaz; traducirlo seria un acto juridico con su
  propia version, no un cambio de cadena.
- **Anadir un idioma** sera anadir `Textos.<cultura>.resx` al lado y la cultura en `Program.cs`, sin
  tocar ninguna vista.

---

## ADR-0020 — Interactividad global de Blazor, con las paginas publicas excluidas

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: medio, define como se sirve cada pagina

`arquitectura.md` describia interactividad **pagina a pagina**: SSR estatico en lo publico,
`InteractiveServer` en la gestion. Al montar la cascara resulto que eso **no puede funcionar**: un
*layout* no hereda el modo de render de su pagina. Con `@rendermode` en la pagina, la pagina es
interactiva pero su cascara no, y el boton que abre el cajon lateral queda pintado sin hacer nada.

- **Decision**: interactividad **global** (`<Routes @rendermode="...">`), y las paginas que deben
  servirse estaticas lo dicen con `@attribute [ExcludeFromInteractiveRouting]`. `App.razor` consulta
  `HttpContext.AcceptsInteractiveRouting()` y pasa `null` como modo en esas peticiones.
- **Que se excluye y por que**:
  - *Registro, acceso y alta con Google*: entrar escribe la cookie de sesion, y eso necesita el
    `HttpContext` de una peticion de verdad, que no existe dentro de un circuito.
  - *Portada, aviso de privacidad, error y pagina no encontrada*: para **no abrir un circuito de
    SignalR por cada visita anonima**. El nivel gratuito de Azure SignalR son unas 20 conexiones
    simultaneas en total; gastarlas en gente leyendo la portada dejaria sin sitio a los jugadores
    de un torneo en curso. La busqueda publica del hito 5 se excluira por lo mismo.
- **Dos cascaras**: `MainLayout` (publica, barra superior y poco mas) y `StoreLayout` (gestion, con
  cajon lateral, dialogos y avisos emergentes). Estan separadas porque la publica se pinta tambien
  sin interactividad, y ahi un cajon que se abre no tendria quien lo abriera.
- **Hay dos pruebas** que fijan esto: las paginas publicas no traen marca de isla interactiva en el
  HTML, y `/tienda` si.

---

## ADR-0021 — Los formularios de SSR estatico llevan `name` y `Text` a mano, y la casilla es de Blazor

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, tres formularios

En SSR estatico no hay circuito: el navegador envia el formulario y el servidor lo reconstruye a
partir del atributo `name` de cada campo. Los campos de MudBlazor no son los `InputText` de Blazor,
asi que no lo ponen solos.

- **Campos de texto**: `MudTextField` con `name="Input.Email"` **y** `Text` con el valor. El `name`
  es por donde entra el dato; `Text` es lo unico que hace que se dibuje el atributo `value`, y sin
  el un error de validacion devolveria el formulario con todo borrado.
- **Casillas**: se usa el `InputCheckbox` de **Blazor**, no `MudCheckBox`. Una casilla HTML sin
  atributo `value` envia la cadena `"on"` al marcarse, y el enlace de formulario de Blazor no la
  acepta para un booleano (*"The value 'on' is not valid"*). MudBlazor no deja poner ese `value`
  porque el atributo choca con su propio parametro `Value`. Importa porque esa casilla es la del
  consentimiento de proteccion de datos: si no llegase, el alta se rechazaria siempre.
- Las dos cosas estan encapsuladas en `SsrTextField` y `SsrCheckBox`, y **fijadas con pruebas** que
  renderizan los componentes sin base de datos, para que una version futura de MudBlazor que deje de
  reenviar el atributo rompa una prueba en vez de romper el registro en silencio.

---

## ADR-0022 — Las politicas nombradas se autentican solo con la cookie

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo · **Amplia**: ADR-0013

`CanOrganize`, `IsSuperadmin`, `IsPlayer` y la politica por defecto aceptaban **cookie y Bearer**.
Al proteger la primera pagina se vio el problema: cuando se rechaza el acceso, ASP.NET lanza el
*challenge* de **todos** los esquemas de la politica. El de la cookie escribe una redireccion y el
de Bearer un 401, y la respuesta salia con **las dos cosas a la vez**, que no es nada que un
navegador sepa interpretar.

- **Decision**: las politicas nombradas declaran solo `IdentityConstants.ApplicationScheme`. Los
  endpoints de la API ya declaraban Bearer explicitamente uno a uno (ADR-0013), asi que la regla
  queda uniforme: **cada puerta acepta una sola llave**.
- Se configura ademas adonde manda la cookie a quien no pasa: `/cuenta/acceso` si le falta sesion
  (con el parametro `volver`) y `/sin-permiso` si le falta permiso. Por defecto apuntaba a
  `/Account/Login`, que es de la plantilla de ASP.NET y aqui no existe.
- La pagina de acceso **solo acepta destinos dentro del sitio**: un enlace de phishing podria poner
  cualquier cosa en `volver`, y rebotar a otro dominio justo despues de teclear la contrasena es
  precisamente lo que se busca al montar uno.

---

## ADR-0023 — La comprobacion de tienda aprobada consulta en su propio ambito

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, pero sienta precedente

`StoreDirectory` usaba el `TopCutDbContext` de la peticion. La primera pagina de gestion reventaba
al abrirse: *"a second operation was started on this context instance"*. La politica `CanOrganize`
la evaluan a la vez el endpoint y el `AuthorizeView` de la barra superior, Blazor los renderiza en
paralelo, y un `DbContext` **no admite dos operaciones simultaneas**.

- **Decision**: `StoreDirectory` abre su propio ambito de DI y su propio `DbContext` por consulta.
  Es una lectura unica por indice, asi que sale barato.
- **Alternativa descartada**: cachear la respuesta en la peticion. No arregla la carrera (dos
  comprobaciones simultaneas empiezan antes de que ninguna cachee) y ademas pierde lo que da valor a
  ADR-0011: que revocar una tienda surta efecto en la peticion siguiente.
- **Precedente para el hito 3b**: cualquier componente interactivo que consulte datos tiene el mismo
  problema. Si varios componentes de una pantalla van a leer a la vez, el acceso a datos tendra que
  dejar de compartir el contexto de la peticion (`IDbContextFactory` o un ambito por operacion).

---

## ADR-0024 — La tienda en contexto tambien se fija desde el circuito de Blazor

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: medio · **Amplia**: ADR-0011

`ITenantContext` sacaba la tienda del claim del usuario **leyendo el `HttpContext`**. Eso vale para
una peticion HTTP, pero una pagina interactiva vive dentro de un circuito de SignalR y **ahi no hay
`HttpContext`**. Combinado con que el filtro de tenant falla cerrado, el resultado habria sido el
peor posible: la tienda abre su lista de torneos y la ve **vacia**, sin ningun error.

- **Decision**: `TenantContext` mantiene el camino HTTP y anade `ITenantScope.Set(...)`.
  `StoreLayout` —la cascara por la que pasan todas las pantallas de gestion— la fija una vez, leida
  del **mismo claim** que lee el camino HTTP. El layout se renderiza entero antes que la pagina que
  lleva dentro, asi que el ambito esta puesto antes de la primera consulta.
- **Alternativa descartada**: leer `AuthenticationStateProvider` dentro del `ITenantContext`. Es
  asincrono y el filtro de consulta de EF Core lo necesita sincrono; bloquear sobre una tarea dentro
  de un circuito es pedir un interbloqueo.
- **Alternativa descartada**: un `CircuitHandler`. Es el gancho del framework, pero el estado de
  autenticacion no esta garantizado al abrirse el circuito, y depender de ese orden es fragil.
- **Alternativa descartada**: pasar el id de tienda como parametro en cada caso de uso. Explicito,
  pero renuncia al ambito implicito que hace que **no se pueda olvidar** un filtro, que es el motivo
  entero del ADR-0011.
- **No amplia el acceso de nadie**: el valor sale del principal ya autenticado, las paginas estan
  detras de `[Authorize(CanOrganize)]`, y si nadie lo fija el filtro sigue fallando cerrado.

---

## ADR-0025 — La hora que teclea la tienda se interpreta en una zona horaria configurada

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo, una clase y una clave de configuracion

El formulario de torneo pide fecha y hora; el dominio guarda un `DateTimeOffset`. Convertir con la
zona horaria **de la maquina** habria funcionado en el portatil (Madrid) y habria fallado en
produccion: App Service corre en **UTC**, asi que el mismo "18:00" tecleado en el mismo formulario
habria significado dos instantes distintos y el torneo habria empezado dos horas tarde.

- **Decision**: `LocalTime` convierte en ambos sentidos usando una zona **de configuracion**
  (`TopCut:TimeZone`), con `Europe/Madrid` por defecto, que es donde esta la tienda de la v1.
- **Alternativa descartada**: pedir la zona al navegador por JS. Es lo correcto cuando hay usuarios
  repartidos por el mundo; para una tienda local anade una llamada a JavaScript y un caso de fallo a
  cambio de nada.
- **Alternativa descartada**: guardar y mostrar todo en UTC. Obligaria al organizador a hacer la
  resta mentalmente cada vez que programa un torneo.
- Cuando haya tiendas en varias zonas, lo natural sera que la zona sea un campo de la tienda en vez
  de configuracion global. El cambio afecta a una clase.

---

## ADR-0026 — Lecturas y escrituras separadas: proyecciones para las listas, agregado para los cambios

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: medio, define como consultan las pantallas

El repositorio carga el agregado **entero** (participantes, rondas, emparejamientos y partidas)
porque emparejar y clasificar lo necesitan. Pintar una lista de treinta torneos con eso seria traer
media base de datos para mostrar una fecha y un contador.

- **Decision**: dos caminos.
  - `ITournamentRepository` para **cambiar**: carga el agregado, aplica la operacion y guarda **solo
    si sale bien**, todo en una unidad de trabajo con su propia conexion. Una regla rota a mitad no
    deja rastro, ni siquiera de lo que se aplico antes de romperse.
  - `ITournamentQueries` e `ITcgCatalog` para **leer**: proyecciones sin seguimiento, con los
    contadores calculados por SQL Server.
- Ninguno de los dos usa `IgnoreQueryFilters`, asi que el aislamiento entre tiendas sigue siendo del
  filtro y no de que alguien se acuerde de anadir un `Where`.
- **Guardar es atomico por pantalla**: la pantalla de edicion manda datos y reglas en **una** sola
  llamada. Partido en cuatro, un torneo podria quedarse con las plazas nuevas y la duracion de ronda
  vieja porque la tercera llamada fue rechazada.

---

## ADR-0027 — Las pantallas en vivo van por el circuito de Blazor, no por un hub propio

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: medio · **Cambia**: el plan del hito 4

El roadmap del hito 4 decia "hub de SignalR". Al montarlo se ve que **para la web no hace falta, y
ademas seria contraproducente**: toda pantalla de TopCut es una pagina de Blazor Server, y una
pagina de Blazor Server **ya tiene un WebSocket abierto** con el servidor. Anadirle un hub propio
significaria **dos conexiones por espectador**, y el nivel gratuito de App Service permite **cinco
en total**.

- **Decision**: un difusor en memoria (`ITournamentBroadcaster` / `ITournamentWatch`). Quien guarda
  un cambio levanta su evento de dominio de siempre; un handler avisa al difusor; las pantallas que
  esten mirando ese torneo se enteran por **su propio circuito**, que es el que ya tenian abierto.
- **El mensaje no lleva datos**, solo "este torneo se ha movido". La pantalla vuelve a leer con la
  consulta que ya usa, asi que hay **una sola descripcion** de lo que enseña cada pantalla y quien
  se reconecta tras perderse un aviso acaba en el estado actual y no en un remiendo.
- **Primer suscriptor de los eventos de dominio.** Hasta ahora se levantaban y se tiraban a
  proposito (ADR-0010); esta es la prueba de que la costura era de verdad.
- **Se anade `RoundClosed`**, que faltaba: sin el, la pantalla de la sala seguia enseñando una ronda
  ya cerrada, con su reloj corriendo, hasta que alguien recargaba.
- **Que pasa con el hub**: sigue teniendo sentido, pero **para clientes externos** — la futura app
  movil, que no tiene circuito. Construirlo ahora seria una superficie sin nadie que la consuma y
  sin forma de probarla de verdad, asi que va con la Web API externa (hito 8) y la fase 2.
- **En produccion** se delega en Azure SignalR Service, y eso vale igual: lo que se delega son los
  **circuitos de Blazor**, que es justo lo que resuelve el limite de cinco WebSockets.
- **Limite conocido**: el difusor es de un solo proceso. Con varias instancias de App Service cada
  una avisaria solo a sus propias pantallas. En el plan gratuito hay una sola instancia; el dia que
  haya mas, esto pasa a ser un backplane (Azure SignalR o Redis) detras de la misma interfaz.

---

## ADR-0028 — El cronometro cuenta en el navegador desde un instante del servidor

**Fecha**: 2026-09-20 · **Estado**: aceptada · **Impacto**: bajo

El servidor manda **el instante de fin en UTC y su propia hora**; el navegador calcula una vez la
diferencia entre los dos relojes y a partir de ahi lleva la cuenta atras el solo.

- **Alternativa descartada**: un temporizador por circuito en el servidor. Serian un render y un
  diff por segundo **y por espectador**, y en el nivel gratuito de Azure SignalR (~20.000 mensajes
  al dia) una sola pantalla abierta una tarde se comeria la cuota entera.
- **Alternativa descartada**: mandar los segundos que quedan. Recargar la pagina reiniciaria el
  reloj, que es exactamente lo que `docs/arquitectura.md` dice que no puede pasar.
- **Por que tambien se manda la hora del servidor**: un portatil con la hora mal puesta veria un
  tiempo equivocado si comparase el instante de fin con su propio reloj. Con la diferencia medida
  una vez, el cronometro es correcto aunque el reloj del equipo no lo sea.
- El primer valor se pinta **desde el servidor**, asi que la sala ve un tiempo antes de que el
  JavaScript tome el relevo, y la pagina sigue diciendo algo util sin JavaScript.
