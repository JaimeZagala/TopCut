# TopCut

Plataforma multi-tenant para organizar y gestionar torneos y ligas de juegos de cartas
coleccionables (TCG). La v1 arranca con **Magic: The Gathering** y **One Piece Card Game**.

> Estado: **hito 0 — esqueleto de la solucion**. Todavia no hay logica de negocio.

## Documentacion

La documentacion viva vive en [`/docs`](docs/) y se actualiza en cada hito:

| Documento | Contenido |
|---|---|
| [`docs/vision.md`](docs/vision.md) | Producto, alcance de la v1 y roles |
| [`docs/arquitectura.md`](docs/arquitectura.md) | Capas, stack, infraestructura y despliegue |
| [`docs/decisiones.md`](docs/decisiones.md) | Registro de decisiones (ADR) |
| [`docs/modelo-datos.md`](docs/modelo-datos.md) | Entidades y relaciones |
| [`docs/api.md`](docs/api.md) | Contrato de la Web API para consumo externo |
| [`docs/roadmap.md`](docs/roadmap.md) | Estado de cada hito |

## Estructura

```
TopCut.slnx
src/
  TopCut.Domain/          entidades y reglas de negocio puras
  TopCut.Application/     casos de uso
  TopCut.Infrastructure/  EF Core, SQL Server, Identity, integraciones
  TopCut.Api/             controladores REST + hubs de SignalR (biblioteca)
  TopCut.Web/             Blazor Web App; aloja tambien TopCut.Api
tests/
  TopCut.Domain.Tests/
  TopCut.Application.Tests/
  TopCut.IntegrationTests/
```

`TopCut.Api` es una **biblioteca**, no una aplicacion: `TopCut.Web` la aloja, de modo que la UI y
los endpoints `/api/*` se sirven desde un unico proceso y un unico despliegue
(ver [`docs/decisiones.md`](docs/decisiones.md) ADR-0004).

## Requisitos

- .NET SDK **10.0.401** o superior (ver [`global.json`](global.json))
- SQL Server LocalDB (incluido con Visual Studio) para desarrollo local
- Docker Desktop, solo para las pruebas de integracion con Testcontainers

## Comandos

```bash
dotnet build TopCut.slnx
```

```bash
dotnet test TopCut.slnx
```

```bash
dotnet run --project src/TopCut.Web
```
