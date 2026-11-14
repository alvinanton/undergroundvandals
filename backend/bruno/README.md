# Colección Bruno: Underground Vandals API

Colección nativa con **23 requests exclusivamente del backend .NET**. Abrir `backend/bruno/` mediante **Open Collection**. [Documentación de la API](../docs/api/README.md).

## Preparación

1. Seleccionar **Local** (`http://localhost:5098`) o **Docker** (`http://localhost:5050`). Para otro servidor, duplicar un entorno y cambiar `baseUrl`, sin slash final ni `/api`.
2. Configurar `email` y el secreto `password` con una cuenta existente. Para el primer administrador, seguir [bootstrap](../docs/api/auth-users.md#administrador-inicial).
3. Ejecutar **Auth / Sign in**. Captura `token` en memoria para las requests con Bearer heredado de `collection.bru`. **Auth / Get session** consulta los claims del token.
4. Usar IDs reales o crear recursos para capturar `postId`, `assetId`, `articleId`, `articleSlug` y `userId`. Los UUID incluidos son ejemplos; las capturas de runtime se pierden al cerrar la sesión.
5. Para crear usuarios, revisar `newUsername`, `newEmail`, `newRole` y configurar el secreto `newPassword`. Emails y slugs duplicados se rechazan.

Los secretos `password`, `token` y `newPassword` se declaran sin valor. También puede suministrarse un token manual como secreto de entorno. El login borra el token de runtime anterior y guarda el nuevo solo si tiene éxito; eliminar tokens manuales obsoletos para evitar usarlos como fallback.

## Carpetas y documentación

| Carpeta | Acceso |
| --- | --- |
| Auth | Sign in público; Get session con Bearer |
| Users | Admin |
| Media | Lecturas públicas; escrituras según rol |
| Gallery | Lecturas públicas; escrituras según rol |
| Visits | Público |

Todas las requests usan `{{baseUrl}}` y apuntan a la API .NET. El título visible, el nombre descriptivo del archivo y el encabezado Docs coinciden exactamente con `EndpointSummary` en el controlador.

Cada request incluye documentación en inglés en **Docs**: autenticación, parámetros, payload, respuesta, errores y notas. Los filtros se editan en **Params**, se combinan con AND y pueden desactivarse. El filtro isFeatured viene desactivado.

## Archivos y ejecución

Los ejemplos de medios contienen URLs ficticias. Usar **Media / Generate upload authorization**, cargar bytes a Azure y registrar metadatos con **Create post with assets** o **Add assets to a post**. La firma captura `uploadUrl`, `fileUrl` y `publicId`; se pueden usar `{{fileUrl}}` y `{{publicId}}` en el body. Ver el [flujo de carga](../docs/api/media.md#flujo-de-carga).

Cada aserción verifica el escenario exitoso. IDs inexistentes, permisos insuficientes o datos duplicados pueden producir otros estados documentados. Elegir las operaciones que se desean ejecutar y usar recursos de prueba para escrituras y borrados. Repetir un toggle revierte el estado.

Con Bruno CLI, desde esta carpeta:

```sh
bru run "Media/01 - List posts.bru" --env Local
```

## Estándar para nuevos endpoints

- Solo operaciones implementadas por los controladores del backend .NET.
- Carpetas por recurso sin números. Conservar el orden mediante `folder.bru` y `meta.seq`.
- Una operación por archivo `NN - EndpointSummary.bru`. `meta.name` y el encabezado Docs deben coincidir con el `EndpointSummary` descriptivo en inglés del código. Secuencias únicas dentro de cada carpeta.
- URLs `{{baseUrl}}/api/...`; variables camelCase. No versionar credenciales ni identificadores de producción.
- `auth: inherit` para Bearer protegido y `auth: none` para público, según los atributos del controlador.
- Bodies JSON con ejemplos y Content-Type correcto; queries en Params sincronizadas con la URL. Prefijo `~` para filtros desactivados.
- Docs en inglés con secciones `Authentication`, `Parameters`, `Request body`, `Response`, `Errors` y `Notes`. Sincronizar con el código y `backend/docs/api/`.
- Capturar tokens e IDs con `bru.setVar` solo tras respuestas exitosas. Añadir variables a Local y Docker; secretos sin valores versionados.

Desde la raíz del repositorio:

```sh
node backend/bruno/validate.mjs
```

El validador comprueba cobertura exclusiva del backend, títulos contra EndpointSummary, Docs, variables, JSON, scripts, autenticación y secuencias. No realiza peticiones ni requiere el frontend. No sustituye al parser oficial ni comprueba las respuestas de un servidor.
