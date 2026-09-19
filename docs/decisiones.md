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
