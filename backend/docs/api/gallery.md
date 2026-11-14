# Artículos de galería

[Volver al índice](README.md)

## Consultas públicas

| Ruta GET | Parámetros | Respuesta |
| --- | --- | --- |
| `/api/gallery` | `category?: string`, `tag?: string`, `isFeatured?: boolean`, `includeUnpublished?: boolean = false` | 200 `GalleryArticleResponse[]`, por `publishedAt` descendente |
| `/api/gallery/featured` | Ninguno | 200 artículo publicado destacado más reciente; si no hay destacado, el publicado más reciente |
| `/api/gallery/{id}` | `id: GUID` | 200 artículo por ID |
| `/api/gallery/slug/{slug}` | `slug: string` | 200 artículo por slug, comparación sin distinguir mayúsculas |

`category` ignora mayúsculas; `tag` se convierte a minúsculas y busca pertenencia exacta en tags. Los filtros se combinan con AND. Omitir `isFeatured` incluye ambos estados; false filtra solo los no destacados. No hay paginación.

El listado con `includeUnpublished=true`, el detalle por ID y el detalle por slug permiten leer artículos no publicados sin autenticación. El endpoint featured sí exige `isPublished=true` internamente.

Si no existe featured ni ningún publicado: 404 `{"message":"No gallery articles found."}`. Para detalle inexistente: 404 `{"message":"Gallery article not found."}`.

## POST /api/gallery y PUT /api/gallery/{id}

Admin o Editor. POST crea; PUT reemplaza campos editables de un artículo existente (`id: GUID`). Ambos aceptan el mismo conjunto de campos:

```json
{
  "title": "Escena urbana de Guatemala",
  "slug": "escena-urbana-guatemala",
  "subtitle": "Artistas y espacios de la ciudad",
  "category": "EDITORIAL",
  "heroImageUrl": "https://example.com/portada.webp",
  "videoUrl": null,
  "videoSource": "NONE",
  "contentMarkdown": "## La escena\nTexto del artículo.",
  "readingTimeMinutes": 5,
  "subjectName": "Colectivo UV",
  "location": "Ciudad de Guatemala",
  "externalUrl": "https://example.com/artista",
  "eventDate": "2026-12-01T18:00:00Z",
  "isFeatured": false,
  "isPublished": true,
  "shopCollectionSlug": "urban-art",
  "shopCollectionTitle": "Urban Art",
  "tags": ["graffiti", "guatemala"],
  "galleryImageUrls": ["https://example.com/detalle.webp"]
}
```

| Campo | Tipo | Default y comportamiento |
| --- | --- | --- |
| title | string | `""`; POST rechaza título blanco; PUT no tiene esa comprobación explícita |
| slug | string | `""`; si es blanco se genera desde title; de lo contrario se convierte a minúsculas |
| subtitle | string | `""` |
| category | string | `EDITORIAL`; blanco también usa ese valor; se guarda en mayúsculas |
| heroImageUrl | string | `""` |
| videoUrl | string/null | null |
| videoSource | string | `NONE`; blanco usa `NONE`; se guarda en mayúsculas |
| contentMarkdown | string | `""` |
| readingTimeMinutes | int | 5; valores <= 0 se convierten a 5 |
| subjectName, location, externalUrl | string/null | null |
| eventDate | fecha ISO 8601/null | null |
| isFeatured | boolean | false |
| isPublished | boolean | true |
| shopCollectionSlug, shopCollectionTitle | string/null | null; referencia opcional de tienda, sin comprobación externa |
| tags | string[] | []; los elementos se convierten a minúsculas |
| galleryImageUrls | string[] | [] |

Las categorías y fuentes de video son strings libres, sin enum validado. No hay validación de formato URL ni comprobación de existencia de imágenes. El slug automático solo convierte a minúsculas, reemplaza espacios por guiones y elimina `:`, `?`, `!` y `/`; no elimina acentos ni garantiza un slug único por sí mismo.

POST: 201 con artículo y `Location` hacia el detalle. PUT: 200 con artículo, 404 si no existe. Ambos devuelven 400 `{"message":"An article with this slug already exists."}` al detectar slug duplicado. POST sin título: 400 `{"message":"Title is required."}`. La restricción única de base de datos también puede rechazar concurrencia con un error no manejado.

Al crear, `publishedAt` se fija a UTC actual incluso si `isPublished=false`, `createdAt` se genera en la entidad y `updatedAt` es null. PUT conserva `publishedAt` y `createdAt`, y actualiza `updatedAt`. Cuando se destaca un artículo nuevo o previamente no destacado, el controlador quita el destacado a los demás; esta regla no tiene restricción única en base de datos para peticiones concurrentes.

## GalleryArticleResponse

Todas las consultas de detalle, creación y actualización devuelven los campos del ejemplo de escritura más estos campos de solo lectura:

```json
{
  "id": "44444444-4444-4444-8444-444444444444",
  "publishedAt": "2026-11-14T12:00:00Z",
  "createdAt": "2026-11-14T12:00:00Z",
  "updatedAt": null
}
```

Este fragmento muestra solo los campos adicionales; la respuesta real incluye también todos los campos del artículo. `id` es GUID; `publishedAt` y `createdAt` son fechas; `updatedAt` es fecha/null.

## PATCH /api/gallery/{id}/featured

Admin o Editor, GUID y sin body. Invierte el destacado. Si pasa a true, desmarca los demás artículos. Actualiza `updatedAt`.

```json
{
  "message": "Article marked as featured.",
  "id": "44444444-4444-4444-8444-444444444444",
  "isFeatured": true
}
```

200; al desmarcar usa `Article unfeatured.` y false. 404 `Gallery article not found.`. Repetir la llamada invierte el resultado anterior.

## DELETE /api/gallery/{id}

Solo Admin, GUID y sin body. 204 vacío; 404 `Gallery article not found.`. Elimina el registro de artículo; no borra imágenes o videos del almacenamiento ni colecciones externas.
