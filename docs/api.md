# Web API — contrato para consumo externo

> Ultima actualizacion: 2026-09-19 (hito 0)
>
> **Estado: sin implementar.** Este documento describe la *intencion* del contrato. Se rellena con
> los endpoints reales segun se implementen, y es la referencia de cara a la futura app Android.

## Principios

- La API expone **los mismos casos de uso de `TopCut.Application`** que consume la UI Blazor. Cero
  logica de negocio duplicada en los controladores.
- Autenticacion por **tokens Bearer**, independiente de la cookie de sesion que usa Blazor Server.
  Base prevista: los endpoints de Identity API de ASP.NET Core.
- Todas las respuestas son JSON. Los errores siguen **RFC 9457 (`application/problem+json`)**,
  que es lo que ASP.NET Core genera por defecto.
- El `TenantId` **nunca** se acepta como parametro de entrada: se deduce del usuario autenticado o
  de la ruta publica del evento. Un cliente no puede pedir datos de otra tienda cambiando un campo.
- Versionado por prefijo de ruta: `/api/v1/...`, para poder evolucionar sin romper apps ya
  instaladas en el movil de alguien.

## Superficie prevista

Nada de esto existe todavia; es el guion a implementar.

### Publico (sin autenticacion)

| Metodo | Ruta | Proposito |
|---|---|---|
| `GET` | `/api/v1/torneos` | Buscar eventos por ciudad, tienda, TCG y fecha |
| `GET` | `/api/v1/torneos/{id}` | Detalle publico de un torneo |
| `GET` | `/api/v1/torneos/{id}/clasificacion` | Clasificacion actual |
| `GET` | `/api/v1/tcgs` | Catalogo de TCGs activos |

### Jugador autenticado

| Metodo | Ruta | Proposito |
|---|---|---|
| `POST` | `/api/v1/torneos/{id}/inscripciones` | Inscribirse (pasa a lista de espera si esta lleno) |
| `DELETE` | `/api/v1/torneos/{id}/inscripciones/mia` | Desapuntarse |
| `GET` | `/api/v1/mi/torneos` | Historial propio |
| `GET` | `/api/v1/mi/emparejamiento-actual` | Rival, mesa y tiempo restante de la ronda en curso |
| `POST` | `/api/v1/emparejamientos/{id}/resultado` | Reportar el propio resultado |

### Tienda autenticada

| Metodo | Ruta | Proposito |
|---|---|---|
| `POST` | `/api/v1/torneos` | Crear torneo |
| `PATCH` | `/api/v1/torneos/{id}` | Editar configuracion (incluido el top cut, hasta la ronda 1) |
| `POST` | `/api/v1/torneos/{id}/participantes` | Alta manual de jugador sin cuenta |
| `POST` | `/api/v1/torneos/{id}/rondas` | Lanzar la siguiente ronda (genera emparejamientos) |
| `PUT` | `/api/v1/emparejamientos/{id}/resultado` | Corregir un resultado en caso de discrepancia |
| `POST` | `/api/v1/torneos/{id}/cerrar` | Finalizar el torneo |

## Tiempo real (SignalR)

| Hub | Eventos emitidos |
|---|---|
| `/hubs/torneo` | `RondaLanzada`, `EmparejamientosActualizados`, `ResultadoRegistrado`, `CronometroSincronizado`, `ClasificacionActualizada` |

El cronometro se sincroniza enviando el **instante de fin en UTC**, no los segundos restantes: asi
recargar la pagina o reconectar no reinicia la cuenta atras.

## Concurrencia

Las operaciones de escritura sensibles (reportar resultado, ocupar la ultima plaza) usan
concurrencia optimista. Si se detecta un conflicto, la API responde **`409 Conflict`** con un
`problem+json` que explica que el estado cambio, y el cliente debe recargar antes de reintentar.
