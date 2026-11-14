# Colección Bruno: Underground Vandals API

Colección nativa de Bruno, versionable y lista para abrir con **Open Collection** seleccionando esta carpeta (la que contiene `bruno.json`). No importar los `.bru` individualmente como JSON. [Documentación completa de la API](../docs/api/README.md).

Contiene **27 requests**: 22 operaciones .NET y 5 proxies Astro. No incluye llamadas a endpoints del e-commerce externo cuyo servidor no está en este repositorio.

## Preparación

1. Abrir `bruno/` en Bruno y seleccionar **Local** para .NET (`http://localhost:5098`) o **Docker** (`http://localhost:5050`). Para otro servidor, duplicar un entorno y cambiar `baseUrl`, sin `/` final ni `/api`.
2. En el editor de entorno, configurar `email` y el secreto `password` con una cuenta existente. No hay credenciales válidas incluidas. Para crear al primer Admin, seguir [bootstrap](../docs/api/auth-users.md#administrador-inicial).
3. Ejecutar `01 - Auth / Login`. Guarda `token` como variable de runtime, sin escribirlo a disco. Las operaciones privadas heredan Bearer desde `collection.bru`; las públicas usan `auth: none`. Se necesita Admin para usuarios y borrado de publicaciones/artículos; Editor puede hacer las demás escrituras de contenido.
4. Usar IDs reales en el entorno o crear recursos: las creaciones capturan `userId`, `postId`, `assetId`, `articleId` y `articleSlug`. Son variables de runtime y se pierden al cerrar la sesión; copiar al entorno únicamente IDs que se quieran conservar. Los UUID incluidos son ejemplos, no recursos existentes.
5. Para crear usuarios, configurar también `newPassword` como secreto, y revisar `newUsername`, `newEmail` y `newRole`. Repetir creación con el mismo email devuelve 400. Repetir creación de artículo con el mismo slug también devuelve 400.

El login borra el token de runtime anterior antes de guardar uno nuevo exitoso. Los secretos `password`, `newPassword` y `token` están declarados sin valor en los entornos; un token de entorno es opcional si se desea suministrarlo manualmente. Si se configuró uno manualmente, eliminarlo al dejar de usarlo para que no actúe como fallback.

Los ejemplos de medios usan URLs ficticias. Solicitar firma, cargar bytes a Azure y sustituir `assets[].url` y `assets[].publicId` por los reales antes de registrar el asset. La request de firma captura `uploadUrl`, `fileUrl` y `publicId`; se pueden usar `{{fileUrl}}` y `{{publicId}}` al editar el body. El [flujo de carga](../docs/api/media.md#flujo-de-carga) explica el PUT binario a Azure; esa URL temporal no usa `baseUrl` porque pertenece al almacenamiento externo.

## Entornos y carpetas

| Carpeta | Entorno | Acceso |
| --- | --- | --- |
| 01 - Auth | Local / Docker | Login público |
| 02 - Users | Local / Docker | Admin |
| 03 - Media | Local / Docker | Lecturas públicas; escrituras según rol |
| 04 - Gallery | Local / Docker | Lecturas públicas; escrituras según rol |
| 05 - Visits | Local / Docker | Público |
| 06 - Proxies Astro | Frontend (`http://localhost:4321`) | Ver cada request |

Todas las URLs de requests usan `{{baseUrl}}`. Para proxies, cambiar a **Frontend** y ejecutar solo la carpeta 06. No ejecutar Login contra Astro: el endpoint de autenticación pertenece a .NET. Si se pierde el token al cambiar de entorno, copiar un JWT obtenido de .NET al secreto `token` del entorno Frontend. Las requests de detalle/actualización de producto envían la cookie `uv_auth_token={{token}}`; la API key del e-commerce se configura exclusivamente en el servidor Astro. Ver [contrato y límites actuales de estos proxies](../docs/api/visits-proxies.md#proxies-astro).

Las queries opcionales se editan en **Params**. Los listados traen ejemplos de category/tag activos: desactivarlos para ver todos los elementos. `isFeatured` viene desactivado para incluir ambos estados. Los filtros se combinan con AND.

## Ejecución y resultados

Cada request tiene documentación y una aserción de estado para el escenario exitoso; en recursos vacíos, IDs ficticios o permisos insuficientes el resultado puede ser 400/401/403/404. Consultar el contrato para esos escenarios. No se necesita ejecutar escrituras para explorar las lecturas.

La colección es un catálogo de operaciones, no una suite que deba ejecutarse completa en orden: contiene eliminación de archivos, desactivación de usuarios y alternancia de estados. Ejecutar operaciones de escritura solo sobre recursos de prueba elegidos. Crear el recurso antes de consultarlo si se usan las capturas automáticas, y dejar los borrados para el final. Los toggles repetidos revierten el estado.

Si se dispone de Bruno CLI, desde esta carpeta se puede ejecutar una lectura concreta:

```sh
bru run "03 - Media/01 - Listar publicaciones.bru" --env Local
```

## Estándar para nuevos endpoints

- Carpeta por recurso, prefijo `NN - Recurso`, `folder.bru` con secuencia única y estable.
- Una operación por archivo `NN - Accion.bru`; `meta.name` en español, `meta.type: http` y `meta.seq` único dentro de su carpeta. Agregar al final para evitar renumerar requests existentes.
- URLs `{{baseUrl}}/api/...`. Variables de entorno en camelCase; IDs como `postId`, `assetId`, `articleId`, `userId`. Nunca URLs de servidor o credenciales reales en requests.
- `auth: inherit` para backend protegido y `auth: none` para público; no repetir Bearer en cada archivo. Para cookies u otros mecanismos, declarar cabeceras solo donde corresponde.
- Body JSON con todos los campos relevantes y `Content-Type: application/json`. Strings, enums y fechas deben seguir el contrato del DTO, incluidos null/defaults. Agregar ejemplos con datos ficticios.
- Filtros en `params:query`, en sincronía con los habilitados en la URL. Prefijo `~` para filtros desactivados.
- `docs` debe indicar rol, dependencias, parámetros, resultado y errores relevantes. Actualizar también `docs/api/`.
- Capturar IDs y tokens solo tras respuesta exitosa con `bru.setVar`; no persistir respuestas o SAS a disco. Cada request debe incluir una aserción del estado esperado.
- Añadir variables nuevas a los tres entornos. Secretos como nombres en `vars:secret`, sin valores versionados. No reutilizar IDs de producción como ejemplos.

Validación local, sin servidor ni dependencias:

```sh
node bruno/validate.mjs
```

Comprueba inventario frente a controladores y rutas Astro, URLs, autenticación .NET, variables, payloads JSON, scripts y secuencias. Es una comprobación estática; no sustituye al parser oficial de Bruno ni verifica disponibilidad o respuestas reales del servidor.

Referencias de Bruno: [variables de runtime](https://blog.usebruno.com/manage-variables) y [API JavaScript de scripts](https://github.com/usebruno/bruno-docs/blob/main/testing/script/javascript-reference.mdx).
