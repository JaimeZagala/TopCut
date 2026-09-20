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
- **Docker Desktop**, para la base de datos de desarrollo y para las pruebas de integracion

## Configuracion local

Arranca primero la base de datos:

```bash
docker compose up -d
```

Levanta un SQL Server 2022 en el puerto **11433** con los datos en un volumen, asi que sobreviven
entre sesiones. La cadena de conexion ya apunta ahi en `appsettings.Development.json`.

Lo demas son secretos y va en user-secrets, nunca en el repositorio. El `UserSecretsId` ya esta en
`TopCut.Web.csproj`, asi que no hace falta inicializarlo:

```bash
dotnet user-secrets --project src/TopCut.Web set "Superadmin:Email" "tu@email.com"
```

```bash
dotnet user-secrets --project src/TopCut.Web set "Superadmin:Password" "una-contrasena-larga"
```

Sin esas dos claves la aplicacion arranca igual, pero no habra ningun Superadmin y por tanto nadie
que pueda aprobar la primera tienda. El login con Google se activa solo si existen
`Authentication:Google:ClientId` y `Authentication:Google:ClientSecret`.

## La base de datos de desarrollo

| Para | Comando |
|---|---|
| Arrancarla | `docker compose up -d` |
| Pararla conservando los datos | `docker compose down` |
| Empezar de cero | `docker compose down -v` |
| Ver los logs | `docker compose logs -f sqlserver` |

Tras un `down -v`, la aplicacion recrea el esquema y siembra el catalogo al siguiente arranque.

Para mirar dentro sin salir de la terminal:

```bash
docker exec -it topcut-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "TopCut-Local-2026" -C -d TopCut
```

La contrasena esta en claro en `compose.yaml` a proposito: esa base solo escucha en tu localhost y
no contiene nada real. **No la reutilices en ningun otro sitio.**

> **Por que no LocalDB** (ADR-0015): funcionaba, pero es un motor distinto al de produccion, se
> apaga solo cuando lleva rato sin uso y su estado vive en el registro de Windows, donde es facil
> que se desincronice del disco y deje errores desconcertantes. El contenedor es el mismo SQL Server
> que usan las pruebas de integracion y el que hay detras de Azure SQL.

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
