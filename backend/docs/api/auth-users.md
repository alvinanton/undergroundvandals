# Autenticación y usuarios

[Volver al índice](README.md)

## Administrador inicial

En una instalación nueva sin administradores, usar la herramienta de consola independiente; no hay endpoint HTTP de bootstrap. Configurar `ConnectionStrings__DefaultConnection` o `DATABASE_URL` en el entorno del operador con una conexión Npgsql hacia la base de datos ya migrada. La herramienta no carga los archivos `.env` ni la configuración de la API.

Desde la raíz del repositorio, en terminal interactiva:

```sh
dotnet run --project backend/UndergroundVandals/UndergroundVandals.Admin -- bootstrap-admin --email admin@example.com --username Admin
```

Respetar ese orden de argumentos. Introducir y confirmar la contraseña cuando se solicite: mínimo 12 caracteres y máximo 72 bytes UTF-8. No se admite password por argumento, variable de entorno ni entrada redirigida. El nombre debe tener entre 1 y 100 caracteres tras trim; el email debe ser válido y de hasta 254 caracteres. La herramienta normaliza el email a minúsculas y crea un Admin activo con hash BCrypt.

Se necesita permiso para leer/insertar usuarios y bloquear la tabla `Users`. Una transacción y un bloqueo exclusivo serializan la comprobación y creación. Si ya existe cualquier Admin, incluso inactivo, o el email pertenece a otro usuario, rechaza la operación. No recupera cuentas ni cambia passwords ni roles existentes. Si ya hay un Admin, usar login y POST `/api/users`.

Código de salida: 0 éxito; 1 rechazo o fallo de inicialización; 2 invocación, configuración o entrada inválidas. No aplica migraciones ni inicia la API.

## POST /api/auth/login

Público. Payload:

```json
{ "email": "admin@example.com", "password": "contraseña-del-entorno" }
```

`email` y `password` son strings no anulables. El email se compara directamente, sin normalización en login. El password se verifica con BCrypt. Éxito 200:

```json
{ "token": "eyJ...", "username": "Admin", "role": "Admin" }
```

Actualiza `lastLoginAt`. JWT firmado con HS256, vencimiento de 8 horas y claims de ID, nombre, email y rol; valida firma, emisor, audiencia y duración. No hay refresh ni revocación implementados. Desactivar un usuario evita nuevos logins, pero el middleware no vuelve a consultar su estado en cada petición con un JWT existente.

Errores 401: `{"message":"Invalid credentials."}` o `{"message":"Account is deactivated. Please contact an administrator."}`.

## GET /api/users

Solo Admin. Sin parámetros. Devuelve 200 con `UserResponse[]`, ordenado por `createdAt` descendente. El hash y contraseña nunca se incluyen.

```json
[
  {
    "id": "11111111-1111-4111-8111-111111111111",
    "username": "Editor de prueba",
    "email": "editor@example.com",
    "role": "Editor",
    "isActive": true,
    "createdAt": "2026-11-14T12:00:00Z",
    "lastLoginAt": null
  }
]
```

`id`: GUID; `username`, `email`, `role`: string; `isActive`: boolean; `createdAt`: fecha; `lastLoginAt`: fecha o null.

## POST /api/users

Solo Admin. Payload:

```json
{
  "username": "Editor de prueba",
  "email": "editor@example.com",
  "password": "Cambiar-Ejemplo-2026!",
  "role": "Editor"
}
```

`username`, `email` y `password` son strings no anulables. `role` es el enum `Admin` o `Editor`; omitirlo asigna `Editor`. Se serializa como string; el convertidor de enum también permite valores numéricos, por lo que la colección estandariza el uso de nombres. No hay atributos de validación de formato email o longitud de contraseña en este DTO.

201 con `UserResponse` como el elemento del listado y cabecera `Location` generada hacia la acción de listado con el ID como valor de ruta/query; no existe GET de usuario individual. Crea el usuario activo, cifra el password con BCrypt y deja `lastLoginAt` en null. Email ya existente: 400 `{"message":"Email is already registered."}`; la comprobación usa igualdad directa.

## PATCH /api/users/{id}/status

Solo Admin. `id`: GUID de usuario. Payload:

```json
{ "isActive": false }
```

`isActive`: boolean; si se omite, el DTO usa false. Asigna el estado, no lo alterna. 200:

```json
{ "message": "User status successfully updated to Inactive." }
```

Con true el texto final es `Active`. Usuario inexistente: 404 `{"message":"User not found."}`. No hay una comprobación que impida desactivar al propio administrador o al último administrador.
