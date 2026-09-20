# Web API — contrato para consumo externo

> Ultima actualizacion: 2026-09-20 (hito 1c)
>
> **Estado: implementados los endpoints de autenticacion y alta de tienda.** El resto sigue siendo
> la *intencion* del contrato y se rellena segun se implemente.

## Principios

- La API expone **los mismos casos de uso de `TopCut.Application`** que consume la UI Blazor. Cero
  logica de negocio duplicada en los controladores.
- Autenticacion por **tokens Bearer y solo Bearer** (ADR-0013). Aceptar tambien la cookie abriria
  una via de CSRF, y no hace falta: la UI Blazor no llama a la API, invoca los mismos servicios en
  el mismo proceso.
- Todas las respuestas son JSON. Los errores siguen **RFC 9457 (`application/problem+json`)**,
  que es lo que ASP.NET Core genera por defecto.
- El `TenantId` **nunca** se acepta como parametro de entrada: se deduce del usuario autenticado o
  de la ruta publica del evento. Un cliente no puede pedir datos de otra tienda cambiando un campo.
- Versionado por prefijo de ruta: `/api/v1/...`, para poder evolucionar sin romper apps ya
  instaladas en el movil de alguien.

- Las rutas van en **ingles**, como el codigo (ADR-0008): son superficie para desarrolladores, no
  texto que lea un jugador.

## Superficie

### Autenticacion — **implementado**

| Metodo | Ruta | Proposito |
|---|---|---|
| `POST` | `/api/v1/auth/register` | Registra un jugador. **Exige aceptar el aviso de proteccion de datos**; sin eso responde 400. |
| `POST` | `/api/v1/auth/login` | Devuelve un token Bearer. Mismo mensaje de error para contrasena incorrecta y cuenta inexistente, para no permitir enumerar emails. |
| `GET` | `/api/v1/auth/me` | Quien soy: id, email, nombre visible, roles y tienda. |

### Alta y control de tiendas — **implementado**

| Metodo | Ruta | Proposito |
|---|---|---|
| `POST` | `/api/v1/stores/apply` | Solicita el rol de tienda. Crea la tienda **pendiente de aprobacion**. |
| `POST` | `/api/v1/stores/{id}/approve` | Solo Superadmin. Activa la tienda. |
| `POST` | `/api/v1/stores/{id}/revoke` | Solo Superadmin. Le retira el permiso de organizar. |

El claim de tienda se emite **al iniciar sesion**, asi que tras solicitar el alta hace falta un
token nuevo para que aparezca.

### Publico (sin autenticacion)

| Metodo | Ruta | Proposito |
|---|---|---|
| `GET` | `/api/v1/tournaments` | Buscar eventos por ciudad, tienda, TCG y fecha |
| `GET` | `/api/v1/tournaments/{id}` | Detalle publico de un torneo |
| `GET` | `/api/v1/tournaments/{id}/standings` | Clasificacion actual |
| `GET` | `/api/v1/tcgs` | Catalogo de TCGs activos |

### Jugador autenticado

| Metodo | Ruta | Proposito |
|---|---|---|
| `POST` | `/api/v1/tournaments/{id}/registrations` | Inscribirse (pasa a lista de espera si esta lleno) |
| `DELETE` | `/api/v1/tournaments/{id}/registrations/mine` | Desapuntarse |
| `GET` | `/api/v1/me/tournaments` | Historial propio |
| `GET` | `/api/v1/me/current-pairing` | Rival, mesa y tiempo restante de la ronda en curso |
| `POST` | `/api/v1/pairings/{id}/games` | Reportar una partida segun termina |

### Tienda autenticada

| Metodo | Ruta | Proposito |
|---|---|---|
| `POST` | `/api/v1/tournaments` | Crear torneo |
| `PATCH` | `/api/v1/tournaments/{id}` | Editar configuracion (incluido el top cut, hasta la ronda 1) |
| `POST` | `/api/v1/tournaments/{id}/participants` | Alta manual de jugador sin cuenta |
| `POST` | `/api/v1/tournaments/{id}/rounds` | Lanzar la siguiente ronda (genera emparejamientos) |
| `PUT` | `/api/v1/pairings/{id}/result` | Corregir un resultado en caso de discrepancia |
| `POST` | `/api/v1/tournaments/{id}/complete` | Finalizar el torneo |

## Tiempo real (SignalR)

| Hub | Eventos emitidos |
|---|---|
| `/hubs/tournament` | `RoundStarted`, `PairingsUpdated`, `ResultRecorded`, `ClockSynced`, `StandingsUpdated` |

El cronometro se sincroniza enviando el **instante de fin en UTC**, no los segundos restantes: asi
recargar la pagina o reconectar no reinicia la cuenta atras.

## Concurrencia

Las operaciones de escritura sensibles (reportar resultado, ocupar la ultima plaza) usan
concurrencia optimista. Si se detecta un conflicto, la API responde **`409 Conflict`** con un
`problem+json` que explica que el estado cambio, y el cliente debe recargar antes de reintentar.
