# Modelo de datos

> Ultima actualizacion: 2026-09-19 (hito 0)
>
> **Estado: borrador conceptual.** Todavia no hay entidades ni migraciones en el codigo. Este
> documento fija el vocabulario y las relaciones que se implementaran en el hito 1, y se
> actualizara con el esquema real (tipos, indices, restricciones) cuando exista.

## Convenciones

- **Claves primarias mixtas** (ADR-0006): `int` autoincremental en catalogos (`Tcg`, `Ruleset`),
  `Guid` v7 secuencial (`Guid.CreateVersion7()`) en entidades de negocio expuestas en URLs
  (`Tienda`, `Torneo`, `Participante`, `Ronda`, `Emparejamiento`, `Partida`, `Liga`). Los IDs de
  Identity (`AspNetUsers`, `AspNetRoles`) se quedan como `string`, el default del framework.
- `Tenant` y `Tienda` se modelan como **la misma entidad**: una tienda *es* un tenant.
- **Fechas y horas en UTC** en la base de datos (`DateTimeOffset`), convertidas a hora local solo
  al presentarlas. Es lo que hace fiable el cronometro de ronda frente a reconexiones.

## Entidades principales

### Identidad y tenencia

| Entidad | Notas |
|---|---|
| `Tienda` (tenant) | Nombre, direccion **estructurada** (calle, ciudad, provincia, codigo postal), `Latitud`/`Longitud` **opcionales y sin uso en v1**, indicador `EsTiendaDePruebas` (excluida de busquedas publicas), estado de aprobacion por Superadmin. |
| `Usuario` | ASP.NET Core Identity. Email/contrasena o Google. Consentimiento RGPD registrado en el alta. |
| Roles | `Superadmin`, `Tienda`, `Jugador` (roles de Identity). Un usuario con rol `Tienda` esta vinculado a una `Tienda`. |

### Catalogo de juegos

| Entidad | Notas |
|---|---|
| `Tcg` | Catalogo global gestionado por el Superadmin. Activable/desactivable. v1: Magic: The Gathering y One Piece Card Game. |
| `Ruleset` | Configuracion por TCG, cargada como **seed**: formato por fase (Bo1/Bo3 para suizo y para top cut, independientes), puntos por victoria/empate/derrota, criterios de desempate **y su orden**, duracion de ronda por defecto. |

### Torneos

| Entidad | Notas |
|---|---|
| `Torneo` | Pertenece a una `Tienda` (`TenantId`) y a un `Tcg`. Nombre, fecha, direccion del evento, plazas, estado. **Copia** la configuracion del ruleset al crearse (no la referencia en vivo), de modo que cambiar el ruleset no altera torneos ya creados. Top cut opcional: activable/desactivable y de tamano variable **hasta que se lanza la primera ronda**. |
| `Participante` | Propio de cada torneo. Vinculo **opcional** a un `Usuario`: la tienda puede dar de alta jugadores sin cuenta. Estado: inscrito / en lista de espera / **dado de baja** (retirado: excluido de emparejamientos futuros, conserva resultados jugados). |
| `Ronda` | Numero, fase (suiza o top cut), instante de inicio y de fin previsto (fuente de verdad del cronometro), estado. |
| `Emparejamiento` | Dos participantes (o uno + **bye**) dentro de una ronda. Resultado global y `RowVersion` para concurrencia optimista. |
| `Partida` | **Cada partida individual** dentro de un emparejamiento a Bo3. Necesaria para el desempate por % de partidas ganadas. En Bo1 hay una sola. |

### Ligas

| Entidad | Notas |
|---|---|
| `Liga` / `Temporada` | Pertenece a una `Tienda` y a un `Tcg`. Agrupa varios torneos y **acumula** puntos a lo largo de ellos usando el mismo modelo de puntuacion. |

## Relaciones (esquema conceptual)

```
Tienda 1──* Torneo *──1 Tcg 1──1 Ruleset
   │            │
   │            ├──* Participante ──0..1 Usuario
   │            │
   │            └──* Ronda ──* Emparejamiento ──* Partida
   │
   └──* Liga ──* Torneo
```

## Reglas transversales

- **Multi-tenancy**: toda entidad que pertenezca a una tienda lleva `TenantId` y se filtra con
  `HasQueryFilter` de EF Core. Ningun caso de uso debe filtrar el tenant a mano.
- **Concurrencia optimista**: columna `RowVersion` en `Emparejamiento` / `Partida` (reportar
  resultado) y en las entidades implicadas en ocupar la ultima plaza o promover desde la lista de
  espera.
- **Auditoria minima**: fecha de creacion y de ultima modificacion en las entidades editables, para
  poder reconstruir que paso si un resultado se corrige a mano en mitad de un torneo.

## Pendiente para el hito 1

- Fijar los valores exactos de puntos y desempates de los rulesets de Magic y One Piece
  **contra sus reglas oficiales vigentes**, y dejar la fuente citada en el seed.
- Decidir si `Liga` referencia torneos existentes o si un torneo declara a que liga pertenece
  (afecta a si un torneo puede puntuar para dos ligas a la vez).
