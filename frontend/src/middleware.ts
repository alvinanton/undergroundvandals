// src/middleware.ts
import { defineMiddleware } from 'astro:middleware';

export const onRequest = defineMiddleware((context, next) => {
  const token = context.cookies.get('uv_auth_token')?.value;

  if (token) {
    try {
      const parts = token.split('.');
      if (parts.length === 3) {
        // Safe Base64URL decode for Edge/Vercel runtime
        const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(
          atob(base64)
            .split('')
            .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
            .join('')
        );

        const payload = JSON.parse(jsonPayload);
        const role =
          payload.role ||
          payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
          'Editor';
        const email =
          payload.email ||
          payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
          '';

        context.locals.user = { role, email };
      } else {
        context.locals.user = null;
      }
    } catch {
      context.locals.user = null;
    }
  } else {
    context.locals.user = null;
  }

  return next();
});