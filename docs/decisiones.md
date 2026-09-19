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
