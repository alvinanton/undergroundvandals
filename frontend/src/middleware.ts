import { defineMiddleware } from 'astro:middleware';
import { authResponse, SESSION_COOKIE, setSessionCookie, siteOrigin, validateSession } from './lib/server/auth';

export const onRequest = defineMiddleware(async (context, next) => {
  context.locals.user = null;
  let path: string;
  try {
    path = decodeURIComponent(context.url.pathname).replace(/\/+$/, '') || '/';
  } catch {
    return authResponse('Invalid request path.', 400);
  }

  const adminPage = path === '/admin' || path.startsWith('/admin/');
  const adminApi = path === '/api/admin' || path.startsWith('/api/admin/');
  const authApi = path === '/api/auth' || path.startsWith('/api/auth/');
  if (!adminPage && !adminApi && !authApi) return next();

  const redirect = (destination: string) => {
    const response = context.redirect(destination);
    response.headers.set('Cache-Control', 'no-store');
    return response;
  };

  if (!['GET', 'HEAD', 'OPTIONS'].includes(context.request.method) &&
      (context.request.headers.get('origin') !== siteOrigin(context.url) ||
       context.request.headers.get('sec-fetch-site') === 'cross-site')) {
    return authResponse('Requests must originate from this site.', 403);
  }

  if (!authApi) {
    const token = context.cookies.get(SESSION_COOKIE)?.value;
    if (token) {
      try {
        context.locals.user = await validateSession(token);
      } catch {
        return authResponse('Authentication service unavailable. Please try again.', 503);
      }

      if (context.locals.user) {
        setSessionCookie(context.cookies, context.url, token, context.locals.user.expiresAt);
      } else {
        context.cookies.delete(SESSION_COOKIE, { path: '/' });
      }
    }

    if (!context.locals.user && path !== '/admin/login') {
      return adminApi ? authResponse('Authentication required.', 401) : redirect('/admin/login');
    }

    const requiresAdmin = path === '/admin/users' || path === '/admin/products' || path === '/admin/pages' ||
      (adminApi && !path.startsWith('/api/admin/content/'));
    if (requiresAdmin && context.locals.user?.role !== 'Admin') {
      return adminApi ? authResponse('Administrator access required.', 403) : redirect('/admin');
    }
  }

  const response = await next();
  response.headers.set('Cache-Control', 'no-store');
  return response;
});
