import { defineMiddleware } from 'astro:middleware';

export const onRequest = defineMiddleware(async (context, next) => {
  const { url, cookies, redirect } = context;

  if (url.pathname.startsWith('/admin')) {
    const token = cookies.get('uv_auth_token')?.value;
    
    // Imprime en la consola de Node/Astro qué ocurre en cada petición
    console.log('--> Middleware en:', url.pathname, '| ¿Hay Token?:', token ? 'SÍ' : 'NO');

    if (url.pathname === '/admin/login') {
      return next();
    }

    if (!token) {
      return redirect('/admin/login');
    }
  }

  return next();
});