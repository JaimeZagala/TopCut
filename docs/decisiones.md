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

## ADR-0008 — Vocabulario del dominio en espanol, fontaneria tecnica en ingles

**Fecha**: 2026-09-19 · **Estado**: aceptada · **Impacto**: medio (renombrado mecanico si se revierte)

Las entidades y conceptos de negocio se nombran en espanol (`Torneo`, `Participante`,
`Emparejamiento`, `Ronda`, `Ruleset`, `Tienda`), y la fontaneria tecnica sigue la convencion del
framework en ingles (`DbContext`, `RowVersion`, `Repository`, `Handler`).

- **Alternativa descartada**: todo el codigo en ingles.
- **Por que**: el lenguaje con el que Jaime y su tienda hablan del problema es espanol, y este
  documento, el resto de `/docs` y el vocabulario del negocio ya lo son. Mantener el mismo termino
  en la conversacion, en la documentacion y en el codigo evita la capa de traduccion mental que
  suele acabar en sinonimos incoherentes (`Match` / `Pairing` / `Enfrentamiento` conviviendo).
- **Matiz**: los identificadores evitan tildes y enes para no depender de la codificacion del
  fichero. Los textos visibles y los comentarios si las usan.
- **Como revertir**: es un renombrado mecanico. Cuanto antes se decida, mas barato; por eso queda
  registrado en el primer hito con codigo.

---

## ADR-0009 — Empate automatico al agotarse la ronda — **PENDIENTE DE CONFIRMAR**

**Fecha**: 2026-09-19 · **Estado**: implementado segun el brief, pendiente de revisar con Jaime

El brief pide que, si el cronometro llega a cero sin resultado reportado, el enfrentamiento se
marque **automaticamente como empate**, con correccion manual posterior del organizador. Asi esta
implementado (`Torneo.AplicarFinDeTiempo`).

**Esto se aparta de los dos reglamentos oficiales**, y conviene saberlo antes de usarlo en un
torneo real:

- **Magic** (MTR): al acabar el tiempo se juegan cinco turnos extra; si al terminarlos nadie ha
  ganado la partida en curso, gana el enfrentamiento **quien vaya por delante en partidas**, y solo
  es empate si van igualados.
- **One Piece** (TRM 5.2): se juegan tres turnos extra con un limite de 5 minutos y, si sigue sin
  haber ganador, se resuelve por criterios de juego (cartas de Life restantes, cartas en el mazo,
  personajes en juego) antes de declarar empate.

Con la regla del brief, un 1-0 en Bo3 al que se le acaba el tiempo se registra como empate cuando
por reglamento seria victoria. **Propuesta para el hito 2**: anadir al ruleset una politica de fin
de tiempo (`EmpateSiempre` frente a `GanaQuienVaPorDelante`) y dejar la del brief como opcion. Se
decide con Jaime antes de implementar el motor.
