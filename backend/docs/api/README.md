# API de Underground Vandals

Referencia del contrato implementado en los controladores .NET y en las rutas Astro del repositorio. Los ejemplos son ilustrativos; sus identificadores y archivos deben reemplazarse por recursos del entorno. No se incluyen contratos del servicio externo de e-commerce cuyo servidor no está en este proyecto.

## Contenido

- [Colección Bruno y convenciones](../../bruno/README.md)
- [Autenticación y usuarios](auth-users.md)
- [Publicaciones, archivos y carga a Azure](media.md)
- [Artículos de galería](gallery.md)
- [Visitas y proxies Astro](visits-proxies.md)

## Inicio local

La API requiere .NET 10, PostgreSQL con las migraciones aplicadas, configuración JWT y Azure Blob Storage. Configurar `ConnectionStrings__DefaultConnection` (o `DATABASE_URL` en formato Npgsql), `JwtSettings__Secret`, `JwtSettings__Issuer`, `JwtSettings__Audience` y `AzureStorageSettings__ConnectionString` mediante configuración local o secretos. No copiar credenciales a la documentación ni a Bruno.

Desde la raíz:

```sh
dotnet ef database update --project backend/UndergroundVandals/UndergroundVandals.Api
dotnet run --project backend/UndergroundVandals/UndergroundVandals.Api --launch-profile http
```

El primer comando requiere la herramienta `dotnet-ef`. El servidor no aplica migraciones ni crea administradores automáticamente. Para una instalación nueva, seguir [el procedimiento de bootstrap](auth-users.md#administrador-inicial). Con un administrador existente, iniciar sesión y crear usuarios mediante la API.

| Entorno | `baseUrl` sin `/` final ni `/api` |
| --- | --- |
| .NET, perfil http | `http://localhost:5098` |
| .NET, perfil https | `https://localhost:7007` |
| .NET, Docker Compose | `http://localhost:5050` |
| Proxies Astro, desarrollo | `http://localhost:4321` |

Fuera de Development se activa redirección HTTPS. CORS de .NET permite cualquier origen, método y cabecera.

## Metadata y documento OpenAPI

En **Development**, `GET /openapi/v1.json` publica el contrato generado desde los controladores; por ejemplo `http://localhost:5098/openapi/v1.json`. El documento incluye las 23 operaciones .NET, sus schemas y autenticación Bearer en las operaciones protegidas. No incluye los proxies Astro. Esta ruta técnica no forma parte de las 23 operaciones funcionales ni de la colección Bruno. No se publica fuera de Development y no se agrega una interfaz Swagger UI.

Cada controlador declara `[Tags]` y cada acción `[EndpointSummary]`, `[EndpointDescription]` y `[ProducesResponseType]`. Toda la metadata debe escribirse en inglés, incluidos resúmenes, descripciones de endpoints, respuestas y seguridad, tags y la información del documento OpenAPI. Estos atributos también funcionan en APIs basadas en controladores; no requieren Minimal APIs. Las respuestas con `message` y los resultados de alternar estados usan DTOs de respuesta para mantener un esquema estable sin cambiar los campos JSON existentes. Los errores 400 que pueden proceder de validación automática o del controlador se describen como cuerpo abierto, pues pueden tener formatos distintos.

La configuración central está en `UndergroundVandals.Api/OpenApi/ApiDocumentationExtensions.cs`; obtiene los requisitos de seguridad de los atributos `[Authorize]` y respeta `[AllowAnonymous]`. Los roles se documentan en la descripción de cada operación. `v1` identifica el documento, sin agregar versionado a las rutas funcionales.

Al crear una acción, agregar resumen, descripción, permisos y tipos/códigos de respuesta junto a los atributos HTTP. Usar el DTO real para las respuestas con cuerpo y `typeof(void)` para las respuestas vacías. No aplicar `[Consumes]` o `[Produces]` como si fueran únicamente documentación: también pueden afectar el tratamiento de contenido en MVC. Los detalles de estos atributos están en la [documentación de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0).

## Convenciones del contrato

- Rutas de .NET: `/api/Auth`, `/api/Users`, `/api/Media`, `/api/Gallery`, `/api/Visits`; se usan minúsculas en los ejemplos, admitidas por el enrutamiento de ASP.NET Core.
- JSON en camelCase; enviar `Content-Type: application/json` cuando hay payload. Fechas en ISO 8601; las fechas generadas por el servidor usan UTC. IDs .NET de tipo UUID/GUID; IDs de productos de los proxies de tipo entero positivo.
- JWT mediante `Authorization: Bearer <token>`. Roles exactos `Admin` y `Editor`. La lectura pública no requiere token. La columna de permisos de cada tabla corresponde a los atributos reales del servidor.
- Los listados de contenido y usuarios devuelven arrays completos, sin paginación ni envoltorio `items`. No hay filtros de búsqueda libre ni orden configurable implementados.
- `PUT` reemplaza los campos editables: enviar todos los valores que se desean conservar. Los valores omitidos toman los defaults del DTO. Los `PATCH` de archivado y destacado invierten el estado y repetirlos lo revierte; el `PATCH` de usuario asigna el booleano enviado.
- No hay endpoints de registro público, refresh token, logout, cambio de contraseña, detalle de usuario ni estadísticas de visitas. `POST /api/auth/seed-admin` fue eliminado y devuelve 404.

## Índice completo de .NET: 23 operaciones

| Método | Ruta | Permisos | Éxito |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | Público | 200 |
| GET | `/api/auth/session` | Admin, Editor | 200 |
| GET | `/api/users` | Admin | 200 |
| POST | `/api/users` | Admin | 201 |
| PATCH | `/api/users/{id}/status` | Admin | 200 |
| GET | `/api/media` | Público | 200 |
| GET | `/api/media/{id}` | Público | 200 |
| GET | `/api/media/upload-signature` | Admin, Editor | 200 |
| POST | `/api/media/upload` | Admin, Editor | 201 |
| POST | `/api/media/{id}/assets` | Admin, Editor | 200 |
| PUT | `/api/media/{id}` | Admin, Editor | 200 |
| PATCH | `/api/media/{id}/archive` | Admin, Editor | 200 |
| DELETE | `/api/media/assets/{assetId}` | Admin, Editor | 204 |
| DELETE | `/api/media/{id}` | Admin | 204 |
| GET | `/api/gallery` | Público | 200 |
| GET | `/api/gallery/featured` | Público | 200 |
| GET | `/api/gallery/{id}` | Público | 200 |
| GET | `/api/gallery/slug/{slug}` | Público | 200 |
| POST | `/api/gallery` | Admin, Editor | 201 |
| PUT | `/api/gallery/{id}` | Admin, Editor | 200 |
| PATCH | `/api/gallery/{id}/featured` | Admin, Editor | 200 |
| DELETE | `/api/gallery/{id}` | Admin | 204 |
| POST | `/api/visits/track` | Público | 200 vacío |

Las otras [7 operaciones Astro con métodos explícitos](visits-proxies.md#proxies-astro) se consumen sobre la URL del frontend. El proxy dinámico `/api/admin/content/[...path]` reenvía operaciones de contenido permitidas y no se cuenta como una operación concreta en este índice.

## Errores y validaciones comunes

| Estado | Significado |
| --- | --- |
| 400 | JSON inválido, validación del DTO o validación explícita del controlador |
| 401 | Credenciales incorrectas, cuenta inactiva al iniciar sesión, JWT ausente/inválido/vencido |
| 403 | JWT válido con rol sin permiso |
| 404 | Recurso inexistente o ruta no encontrada; un segmento inválido para `{id:guid}` no coincide con la ruta |
| 415 | Tipo de contenido de entrada no soportado |
| 500 | Fallos no manejados, por ejemplo almacenamiento, base de datos o restricciones no validadas |

Los errores explícitos normalmente tienen forma `{"message":"Media item not found."}`. Los 401/403 del middleware pueden tener cuerpo vacío. Los errores automáticos de `[ApiController]` tienen formato Problem Details, por ejemplo:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Title": ["The Title field is required."] },
  "traceId": "identificador-generado-por-el-servidor"
}
```

Los textos y claves dependen del validador y la entrada. Los strings no anulables tienen validación implícita de MVC; esto no equivale a validar emails, URLs o reglas de negocio. No hay un manejador global que garantice JSON uniforme para excepciones. Consultar las páginas de cada recurso para las comprobaciones que sí están implementadas.

## Mantenimiento

Al agregar una operación, actualizar el índice, el contrato del recurso, sus ejemplos y el archivo `.bru` correspondiente en el mismo cambio. Mantener los defaults, permisos y códigos sincronizados con los controladores y DTOs; documentar diferencias de implementación sin prometer reglas que el servidor no aplica. Ejecutar `node backend/bruno/validate.mjs` para comprobar cobertura y estructura de la colección.
