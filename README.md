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

## Configuracion local

La cadena de conexion de desarrollo apunta a LocalDB y ya viene en `appsettings.Development.json`.
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

## Si LocalDB se atraganta

Sintoma al arrancar la web: `Cannot create file 'C:\Users\<tu-usuario>\TopCut.mdf' because it
already exists`, o al reves, `Database 'TopCut' already exists`.

Es un vicio de LocalDB, no de la aplicacion: el registro de la base en la instancia y los ficheros
en disco se desincronizan tras un cierre sucio. La base **existe**, y suele bastar con reiniciar la
instancia para que la vuelva a adjuntar:

```bash
sqllocaldb stop MSSQLLocalDB
```

```bash
sqllocaldb start MSSQLLocalDB
```

Para ver que tiene registrado ahora mismo:

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT name, state_desc FROM sys.databases WHERE database_id > 4"
```

Si sigue sin cuadrar, `DROP DATABASE [TopCut]` y borrar despues los `.mdf` y `.ldf` del perfil de
usuario deja la pizarra limpia: la aplicacion recrea el esquema y el catalogo al siguiente arranque.
**No muevas ni renombres esos ficheros mientras la base siga registrada**, porque entonces la
instancia apunta a rutas que ya no existen y el error se vuelve mas confuso.

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
