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

Estas **5 operaciones usan `baseUrl` del frontend**, por ejemplo `http://localhost:4321`. En Bruno seleccionar `Frontend` y ejecutar únicamente la carpeta `06 - Proxies Astro`. El contrato del e-commerce externo no está implementado en este repositorio; las respuestas que se retransmiten y las validaciones del upstream dependen de ese servicio.

| Método | Ruta Astro | Destino del proxy | Acceso actual |
| --- | --- | --- | --- |
| GET | `/api/gallery` | Content API `/api/gallery` | Público |
| GET | `/api/gallery/{slug}` | Content API `/api/gallery/slug/{slug}` | Público |
| GET | `/api/admin/products` | E-commerce `/api/product/admin` | Sin control de rol en la ruta ni en el middleware |
| GET | `/api/admin/products/{id}` | E-commerce `/api/product/admin/{id}` | Cookie con rol Admin en `locals.user` |
| PUT | `/api/admin/products/{id}` | E-commerce `/api/product/{id}` | Cookie con rol Admin en `locals.user` |

### Galería

La URL upstream sale de `PUBLIC_API_URL`, con fallback `http://localhost:5114`. El listado reenvía toda la query; sus filtros válidos son los de [galería](gallery.md). Retransmite estado y JSON del upstream. Si falla la conexión o el parseo JSON: 500 `{"error":"Error connecting to backend API"}` o mensaje de la excepción.

El detalle recibe slug y devuelve 200 artículo; si upstream no responde con éxito, conserva el estado pero cambia el body a `{"error":"Article not found"}`. Si ocurre una excepción devuelve 500 con `error`; sin slug el handler tiene un guard de 400 `Slug is required`, aunque una URL sin segmento normalmente corresponde a la ruta de listado.

### Productos

El proxy agrega `X-Api-Key` desde `ECOMMERCE_ADMIN_API_KEY` en el servidor; el cliente no debe suministrar esa clave. `ECOMMERCE_ADMIN_API_URL` define el upstream, pero los fallbacks difieren: `http://localhost:5114` para listado y `http://localhost:5098` para detalle/PUT. Configurar explícitamente la variable para unificar el destino.

El listado reenvía la query sin validarla. Un ejemplo de consumo del frontend es `?pageNumber=1&pageSize=25`; el contrato efectivo de filtros/paginación depende del e-commerce. En éxito devuelve el JSON upstream; si upstream responde vacío usa `{"items":[],"pageNumber":1,"pageSize":25,"totalItems":0,"totalPages":1}`. Si falla upstream conserva su estado y devuelve ese envoltorio con `error: "Backend HTTP ..."`. Excepciones de red/JSON producen 500 con ese envoltorio y `error`.

Detalle y PUT requieren `id` entero positivo; inválido: 400 `{"message":"Invalid product id."}`. Requieren rol Admin o devuelven 403 `{"message":"Forbidden: admin role required."}`. La autenticación de estas rutas usa la cookie `uv_auth_token`, no la cabecera Bearer. El middleware Astro decodifica los claims del JWT sin verificar firma ni vencimiento; esta es la conducta actual y no equivale a la validación JWT del backend. Sin API key configurada: 500 `{"message":"Server misconfigured: missing admin API key."}`. Excepciones de conexión: 502 `{"message":"Could not reach the e-commerce API."}`. En otros casos retransmiten estado y texto del upstream con Content-Type JSON.

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
