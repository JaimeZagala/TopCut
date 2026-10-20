# TopCut

Plataforma multi-tenant para organizar y gestionar torneos y ligas de juegos de cartas
coleccionables (TCG). La v1 arranca con **Magic: The Gathering** y **One Piece Card Game**.

> Estado: **hitos 0 a 5 cerrados** — la tienda lleva un torneo entero desde el navegador, y los
> jugadores buscan torneos, se inscriben y reportan su resultado desde el móvil. Siguiente: ligas
> por temporada ([`docs/roadmap.md`](docs/roadmap.md)).

## Documentación

La documentación viva vive en [`/docs`](docs/) y se actualiza en cada hito:

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
  TopCut.Infrastructure/  EF Core, PostgreSQL, Identity, integraciones
  TopCut.Api/             controladores REST + hubs de SignalR (biblioteca)
  TopCut.Web/             Blazor Web App; aloja tambien TopCut.Api
tests/
  TopCut.Domain.Tests/
  TopCut.Application.Tests/
  TopCut.IntegrationTests/
```

`TopCut.Api` es una **biblioteca**, no una aplicación: `TopCut.Web` la aloja, de modo que la UI y
los endpoints `/api/*` se sirven desde un único proceso y un único despliegue
(ver [`docs/decisiones.md`](docs/decisiones.md) ADR-0004).

## Requisitos

- .NET SDK **10.0.401** o superior (ver [`global.json`](global.json))
- **Docker Desktop**, para la base de datos de desarrollo y para las pruebas de integración

## Configuración local

Arranca primero la base de datos:

```bash
docker compose up -d
```

Levanta un PostgreSQL 17 en el puerto **15432** con los datos en un volumen, así que sobreviven
entre sesiones. La cadena de conexión ya apunta ahí en `appsettings.Development.json`.

Lo demás son secretos y va en user-secrets, nunca en el repositorio. El `UserSecretsId` ya está en
`TopCut.Web.csproj`, así que no hace falta inicializarlo:

```bash
dotnet user-secrets --project src/TopCut.Web set "Superadmin:Email" "tu@email.com"
```

```bash
dotnet user-secrets --project src/TopCut.Web set "Superadmin:Password" "una-contrasena-larga"
```

Sin esas dos claves la aplicación arranca igual, pero no habrá ningún Superadmin y por tanto nadie
que pueda aprobar la primera tienda. El login con Google se activa solo si existen
`Authentication:Google:ClientId` y `Authentication:Google:ClientSecret`.

## La base de datos de desarrollo

| Para | Comando |
|---|---|
| Arrancarla | `docker compose up -d` |
| Pararla conservando los datos | `docker compose down` |
| Empezar de cero | `docker compose down -v` |
| Ver los logs | `docker compose logs -f postgres` |

Tras un `down -v`, la aplicación recrea el esquema y siembra el catálogo al siguiente arranque.

Para mirar dentro sin salir de la terminal:

```bash
docker exec -it topcut-postgres psql -U topcut -d topcut
```

La contraseña está en claro en `compose.yaml` a propósito: esa base solo escucha en tu localhost y
no contiene nada real. **No la reutilices en ningún otro sitio.**

> **Por qué en Docker** (ADR-0039): es el mismo motor y la misma versión mayor que Neon en
> producción y que las pruebas de integración, sin instalar nada en Windows.

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

## Despliegue

Producción vive en **Render** (la aplicación) y **Neon** (PostgreSQL), los dos en plan gratuito y
sin tarjeta (ADR-0039). Se monta una vez:

1. **Neon**: crea un proyecto con PostgreSQL 17 en la región *AWS Europe Central 1 (Frankfurt)*.
   En *Connect*, elige el formato **.NET** y copia la cadena: es la que empieza por `Host=`, no la
   URL `postgresql://`, que Npgsql no entiende. Usa la conexión directa, no la *pooled*.
2. **Render**: *New > Blueprint*, conecta GitHub y elige este repositorio. Render lee
   [`render.yaml`](render.yaml) y pide los valores que no están en el repositorio:
   `ConnectionStrings__TopCut` (la cadena de Neon), `Superadmin__Email`, `Superadmin__Password` y,
   si se quiere login con Google, `Authentication__Google__ClientId` y
   `Authentication__Google__ClientSecret`.
3. **Google Cloud** (solo si hay login con Google): añade
   `https://<servicio>.onrender.com/signin-google` como URI de redirección autorizada.

A partir de ahí, cada push a `main` se despliega solo. La aplicación aplica las migraciones al
arrancar; antes de fusionar un cambio de esquema, revisa su script en
[`docs/migraciones/`](docs/migraciones/).

El plan gratuito de Render duerme el servicio tras 15 minutos sin visitas y tarda en torno a un
minuto en despertarlo: abre la web un rato antes del torneo.
