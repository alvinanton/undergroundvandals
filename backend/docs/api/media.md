# Publicaciones y archivos

[Volver al índice](README.md)

`Media` administra publicaciones (`Post`) con uno o más assets. La creación recibe metadatos JSON: los bytes del archivo se cargan directamente a Azure, no a `/api/media/upload`.

## Lectura pública

| Método y ruta | Parámetros | Resultado |
| --- | --- | --- |
| GET `/api/media` | `category?: string`, `tag?: string`, `includeArchived?: boolean = false` | 200 `PostResponse[]`, más recientes primero |
| GET `/api/media/{id}` | `id: GUID` | 200 `PostResponse`; 404 `Media item not found.` |

`category` compara sin distinguir mayúsculas. `tag` se convierte a minúsculas y busca pertenencia exacta en `hashtags`; los hashtags no se normalizan al guardarlos, así que se recomienda enviarlos en minúsculas. Filtros combinados usan AND. No hay paginación. `includeArchived=true` es público y el detalle por ID también puede devolver publicaciones archivadas.

Ejemplo de `PostResponse`:

```json
{
  "id": "22222222-2222-4222-8222-222222222222",
  "title": "Mural en la ciudad",
  "description": "Registro de la sesión",
  "category": "Graffiti",
  "hashtags": ["graffiti", "guatemala"],
  "media": [
    {
      "id": "33333333-3333-4333-8333-333333333333",
      "url": "https://example.com/mural.webp",
      "type": "image",
      "isCover": true,
      "thumbnailUrl": null
    }
  ],
  "isArchived": false,
  "createdAt": "2026-11-14T12:00:00Z",
  "mediaAssets": [
    {
      "id": "33333333-3333-4333-8333-333333333333",
      "url": "https://example.com/mural.webp",
      "type": "image",
      "isCover": true,
      "thumbnailUrl": null
    }
  ]
}
```

`media` y `mediaAssets` contienen la misma lista por compatibilidad. El asset de respuesta tiene GUID, URL, tipo `image`/`video`, booleano `isCover` y thumbnail string/null; no expone `publicId`. `description` puede ser null; `hashtags` es un array de strings; `createdAt` es fecha UTC. El orden de assets no se establece explícitamente.

## GET /api/media/upload-signature

Admin o Editor. Query requerida `target`, por ejemplo `archive-media/photos`, `archive-media/videos`, `archive-media/thumbnails` o `gallery-media/articles`. El servicio divide el primer segmento como contenedor y el resto como carpeta; no hay lista cerrada de destinos. Un contenedor sin carpeta también es aceptado por el servicio.

200:

```json
{
  "uploadUrl": "https://cuenta.blob.core.windows.net/archive-media/photos/uuid?sv=...&sig=...",
  "fileUrl": "https://cuenta.blob.core.windows.net/archive-media/photos/uuid",
  "publicId": "archive-media/photos/uuid",
  "provider": "AzureBlob"
}
```

Todos los campos son strings. La SAS permite crear/escribir un blob, empieza 5 minutos antes del momento actual para tolerar desfase y vence 15 minutos después de generarse. El nombre generado es un GUID sin extensión. Se intenta crear el contenedor con acceso público a blobs. `target` vacío: 400 con `Target path (e.g., 'archive-media/photos') is required.`; omitirlo también puede disparar validación automática. Otros errores de Azure no se convierten en 400.

### Flujo de carga

1. Iniciar sesión y solicitar la firma con el destino adecuado.
2. Enviar los bytes a `uploadUrl` mediante PUT con `x-ms-blob-type: BlockBlob` y el MIME en `Content-Type`. Usar la SAS de esa URL como autorización; no se envía el JWT de la API a Azure.
3. Conservar `fileUrl` y `publicId` y registrar los metadatos con POST `/api/media/upload` o POST `/api/media/{id}/assets`. Para una imagen de galería usar su URL en el artículo.

Ejemplo de paso 2, con variables del shell:

```sh
curl -X PUT "$uploadUrl" -H "x-ms-blob-type: BlockBlob" -H "Content-Type: image/webp" --data-binary @mural.webp
```

El frontend implementa optimización WebP y carga por bloques para archivos mayores a 8 MiB en `frontend/src/lib/azureStorage.ts`; 8 MiB es un umbral del cliente, no una validación de la API. Los bloques se suben a la SAS con `comp=block&blockid=...` y se confirman con `comp=blocklist`. No existen rutas HTTP de chunks en esta API .NET. Cloudinary está presente como implementación alternativa, pero el servicio registrado actualmente es Azure Blob.

## POST /api/media/upload

Admin o Editor. Payload completo de ejemplo:

```json
{
  "title": "Mural en la ciudad",
  "description": "Registro de la sesión",
  "category": "Graffiti",
  "hashtags": ["graffiti", "guatemala"],
  "assets": [
    {
      "url": "https://example.com/mural.webp",
      "publicId": "archive-media/photos/uuid",
      "type": "image",
      "isCover": true,
      "thumbnailUrl": null
    }
  ]
}
```

| Campo | Tipo | Reglas/default |
| --- | --- | --- |
| title | string | `[Required]` |
| description | string/null | Opcional |
| category | string | `General` si falta o es blanco |
| hashtags | string[]/null | Opcional; null se guarda como [] |
| assets | AssetInput[] | Requerido, mínimo un elemento |
| assets[].url | string | `[Required]`; sin validación de formato URL |
| assets[].publicId | string | `[Required]`; usar contenedor/ruta del blob para borrarlo después |
| assets[].type | string | `[Required]`; default `image`; `video` sin distinguir mayúsculas crea video, cualquier otro valor crea foto |
| assets[].isCover | boolean | false; no hay regla de portada única |
| assets[].thumbnailUrl | string/null | Opcional; URL del poster/miniatura |

201 con `PostResponse` y `Location` de detalle. No se comprueba que el archivo exista ni se descargan los enlaces. Sin assets: 400 `{"message":"At least one asset is required."}`.

## POST /api/media/{id}/assets

Admin o Editor. `id`: GUID de publicación. Payload `{"assets":[...]}` con los mismos `AssetInput` del ejemplo anterior. Agrega a la lista sin reemplazar assets existentes. 200 `PostResponse`; 400 si la lista está vacía; 404 `Media item not found.` si no existe la publicación.

## PUT /api/media/{id}

Admin o Editor. `id`: GUID. Payload:

```json
{
  "title": "Mural actualizado",
  "description": "Descripción actualizada",
  "category": "Graffiti",
  "hashtags": ["graffiti"]
}
```

Mismas reglas de esos campos que en creación. No actualiza assets ni estado archivado. 200 `PostResponse`; 404 `Media item not found.`.

## PATCH /api/media/{id}/archive

Admin o Editor. GUID, sin body. Invierte `isArchived`. 200:

```json
{
  "message": "Media item successfully archived.",
  "id": "22222222-2222-4222-8222-222222222222",
  "isArchived": true
}
```

Al desarchivar devuelve `Media item successfully unarchived.` y false. 404 `Media item not found.`.

## Borrado

| Operación | Permisos | Efecto y respuestas |
| --- | --- | --- |
| DELETE `/api/media/assets/{assetId}` | Admin, Editor | Borra archivo principal, thumbnail y registro del asset. 204 vacío; 404 `Asset not found.` |
| DELETE `/api/media/{id}` | Admin | Borra archivos principales, thumbnails y publicación con sus assets. 204 vacío; 404 `Media item not found.` |

Ambos IDs son GUID; sin body. El borrado de archivos usa `publicId` y para thumbnails extrae la ruta de la URL. Puede dejar una publicación sin assets. El almacenamiento y la base de datos no comparten transacción: un fallo puede dejar cambios parciales y un resultado false del servicio de borrado no impide eliminar el registro.
