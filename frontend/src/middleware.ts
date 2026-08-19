// src/middleware.ts
import { defineMiddleware } from 'astro:middleware';

export const onRequest = defineMiddleware((context, next) => {
  const token = context.cookies.get('uv_auth_token')?.value;

  if (token) {
    try {
      const payloadBase64 = token.split('.')[1];
      const decodedJson = Buffer.from(payloadBase64, 'base64').toString('utf-8');
      const payload = JSON.parse(decodedJson);
      const role = payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'Editor';
      const email = payload.email || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '';

      context.locals.user = { role, email };
    } catch {
      context.locals.user = null;
    }
  } else {
    context.locals.user = null;
  }

  return next();
});