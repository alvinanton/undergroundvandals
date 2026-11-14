# Visitas y proxies Astro

[Volver al índice](README.md)

## POST /api/visits/track

API .NET, público. Payload:

```json
{
  "path": "/gallery/escena-urbana-guatemala",
  "deviceType": "Desktop",
  "referrer": "https://example.com/"
}
```

Los tres campos son strings anulables y opcionales; `{}` es válido. `path` null/omitido se convierte en `/` y se guarda en minúsculas; una cadena vacía permanece vacía. `deviceType` blanco/null se convierte en `Unknown`; no hay enum validado ni detección automática del dispositivo. `referrer` se guarda tal como llega.

Devuelve **200 sin cuerpo**, no 204 ni JSON. Cada petición agrega un registro sin deduplicación. No hay endpoint HTTP para consultar esas visitas.

Datos obtenidos de cabeceras/conexión:

| Dato | Origen |
| --- | --- |
| IP | Primer valor de `X-Forwarded-For`, luego IP remota, luego `127.0.0.1` |
| userAgent | `User-Agent` |
| countryCode | `CF-IPCountry`, luego `X-AppService-Country`, luego `XX`; mayúsculas |
| region | `CF-RegionCode`, opcional |
| city | `CF-IPCity`, opcional |
| userHash | SHA-256 hexadecimal de `IP_userAgent` |
| createdAt | UTC actual |

La ruta consume estas cabeceras directamente; en despliegues con proxy su integridad depende de la configuración de infraestructura. `VisitTrackingMiddleware.cs` existe pero no está registrado en `Program.cs`; el seguimiento HTTP activo es esta operación explícita.

## Proxies Astro

Estas **7 operaciones usan `baseUrl` del frontend**, por ejemplo `http://localhost:4321`. Estas rutas están fuera de la colección Bruno, que incluye exclusivamente el backend .NET. El contrato del e-commerce externo no está implementado en este repositorio; las respuestas que se retransmiten y las validaciones del upstream dependen de ese servicio.

| Método | Ruta Astro | Destino del proxy | Acceso actual |
| --- | --- | --- | --- |
| GET | `/api/gallery` | Content API `/api/gallery` | Público |
| GET | `/api/gallery/{slug}` | Content API `/api/gallery/slug/{slug}` | Público |
| GET | `/api/admin/products` | E-commerce `/api/product/admin` | Cookie con sesión Admin validada por middleware |
| GET | `/api/admin/products/{id}` | E-commerce `/api/product/admin/{id}` | Cookie con rol Admin en `locals.user` |
| PUT | `/api/admin/products/{id}` | E-commerce `/api/product/{id}` | Cookie con rol Admin en `locals.user` |
| POST | `/api/auth/login` | Content API `/api/auth/login` y `/api/auth/session` | Público |
| POST | `/api/auth/logout` | Elimina la cookie local | Sin requisito explícito en el handler |

### Sesión del frontend

POST `/api/auth/login` recibe `email` y `password` strings no vacíos (máximo 254 y 1024 caracteres respectivamente). Tras autenticar y validar la sesión con la Content API, establece `uv_auth_token` mediante `Set-Cookie`, con HttpOnly, SameSite=Lax y Secure al usar HTTPS. Devuelve 200 `{"message":"Signed in."}`; no devuelve el JWT en el JSON. El cliente HTTP debe conservar la cookie para reutilizar la sesión. Errores: 400 entrada inválida, 401 credenciales inválidas/cuenta inactiva, 502 respuesta upstream inválida o fallida, 503 excepción o timeout.

POST `/api/auth/logout` no recibe body, elimina la cookie en `/` y devuelve 200 `{"message":"Signed out."}`. No revoca el JWT en la API .NET. Ambas respuestas usan `Cache-Control: no-store`.

El middleware exige `Origin` igual al origen del sitio en escrituras de `/api/auth/*` y `/api/admin/*`, y rechaza `Sec-Fetch-Site: cross-site` con 403. El cliente HTTP debe enviar el origen del sitio en la cabecera `Origin`. Las rutas `/api/admin/*` requieren una sesión validada por `/api/auth/session`; las rutas de productos requieren Admin. Sesión ausente/inválida: 401; rol insuficiente: 403; validación de sesión no disponible: 503.

Existe además un proxy dinámico `ALL /api/admin/content/[...path]` con lista permitida de rutas y métodos para operaciones de contenido. No se cuenta entre las siete rutas con métodos explícitos. La colección Bruno representa los contratos de contenido directamente contra la Content API en las carpetas Media, Gallery y Users.

### Galería

La URL upstream sale de `PUBLIC_API_URL`, con fallback `http://localhost:5114`. El listado reenvía toda la query; sus filtros válidos son los de [galería](gallery.md). Retransmite estado y JSON del upstream. Si falla la conexión o el parseo JSON: 500 `{"error":"Error connecting to backend API"}` o mensaje de la excepción.

El detalle recibe slug y devuelve 200 artículo; si upstream no responde con éxito, conserva el estado pero cambia el body a `{"error":"Article not found"}`. Si ocurre una excepción devuelve 500 con `error`; sin slug el handler tiene un guard de 400 `Slug is required`, aunque una URL sin segmento normalmente corresponde a la ruta de listado.

### Productos

El proxy agrega `X-Api-Key` desde `ECOMMERCE_ADMIN_API_KEY` en el servidor; el cliente no debe suministrar esa clave. `ECOMMERCE_ADMIN_API_URL` define el upstream, pero los fallbacks difieren: `http://localhost:5114` para listado y `http://localhost:5098` para detalle/PUT. Configurar explícitamente la variable para unificar el destino.

El listado reenvía la query sin validarla. Un ejemplo de consumo del frontend es `?pageNumber=1&pageSize=25`; el contrato efectivo de filtros/paginación depende del e-commerce. En éxito devuelve el JSON upstream; si upstream responde vacío usa `{"items":[],"pageNumber":1,"pageSize":25,"totalItems":0,"totalPages":1}`. Si falla upstream conserva su estado y devuelve ese envoltorio con `error: "Backend HTTP ..."`. Excepciones de red/JSON producen 500 con ese envoltorio y `error`.

Detalle y PUT requieren `id` entero positivo; inválido: 400 `{"message":"Invalid product id."}`. Requieren rol Admin o devuelven 403 `{"message":"Forbidden: admin role required."}` si el handler recibe un rol insuficiente. La autenticación de estas rutas usa la cookie `uv_auth_token`, no la cabecera Bearer. El middleware Astro valida la sesión consultando la API .NET antes de ejecutar el handler. Sin API key configurada: 500 `{"message":"Server misconfigured: missing admin API key."}`. Excepciones de conexión: 502 `{"message":"Could not reach the e-commerce API."}`. En otros casos retransmiten estado y texto del upstream con Content-Type JSON.

PUT reenvía el body de texto sin validación local. Ejemplo ilustrativo basado en el formulario `ProductsManager.astro` (el esquema obligatorio y las reglas finales deben consultarse en el e-commerce):

```json
{
  "id": 1,
  "name": "Print urbano",
  "description": "Impresión de arte urbano",
  "shortDescription": "Edición de prueba",
  "price": 150,
  "categoryId": 1,
  "isDigital": false,
  "stockQuantity": 10,
  "fileUrl": null,
  "fileSize": null,
  "thumbnailUrl": "https://example.com/print.webp",
  "isActive": true,
  "media": [
    {
      "mediaUrl": "https://example.com/print.webp",
      "storageKey": "products/print.webp",
      "mediaType": 1,
      "mimeType": "image/webp",
      "fileSize": 1024,
      "isMain": true,
      "displayOrder": 1
    }
  ]
}
```

No hay POST ni DELETE de productos dentro de estas rutas Astro. Las llamadas directas a endpoints externos de productos, pedidos, pagos o carga de e-commerce que aparecen en el frontend no son operaciones implementadas por esta API.
