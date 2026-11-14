import type { APIRoute } from 'astro';
import { authResponse, contentApiUrl, SESSION_COOKIE } from '../../../../lib/server/auth';

const guid = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}';
const routes = [
  { pattern: /^media$/, methods: ['GET'] },
  { pattern: /^media\/upload-signature$/, methods: ['GET'] },
  { pattern: /^media\/upload$/, methods: ['POST'] },
  { pattern: new RegExp(`^media/${guid}$`), methods: ['GET', 'PUT', 'DELETE'] },
  { pattern: new RegExp(`^media/${guid}/assets$`), methods: ['POST'] },
  { pattern: new RegExp(`^media/${guid}/archive$`), methods: ['PATCH'] },
  { pattern: new RegExp(`^media/assets/${guid}$`), methods: ['DELETE'] },
  { pattern: /^gallery$/, methods: ['GET', 'POST'] },
  { pattern: new RegExp(`^gallery/${guid}$`), methods: ['GET', 'PUT', 'DELETE'] },
  { pattern: new RegExp(`^gallery/${guid}/featured$`), methods: ['PATCH'] },
  { pattern: /^users$/, methods: ['GET', 'POST'] },
  { pattern: new RegExp(`^users/${guid}$`), methods: ['PUT', 'DELETE'] },
  { pattern: new RegExp(`^users/${guid}/status$`), methods: ['PATCH'] },
];

export const ALL: APIRoute = async ({ params, request, cookies, locals, url }) => {
  if (!locals.user) return authResponse('Authentication required.', 401);
  const path = params.path || '';
  const route = routes.find(candidate => candidate.pattern.test(path));
  if (!route) return authResponse('Not found.', 404);
  if (!route.methods.includes(request.method)) return authResponse('Method not allowed.', 405);
  const requiresAdmin = path.startsWith('users') ||
    (request.method === 'DELETE' && !path.startsWith('media/assets/'));
  if (requiresAdmin && locals.user.role !== 'Admin') return authResponse('Administrator access required.', 403);
  try {
    const headers = new Headers({ Authorization: `Bearer ${cookies.get(SESSION_COOKIE)!.value}` });
    const contentType = request.headers.get('content-type');
    if (contentType) headers.set('Content-Type', contentType);
    const upstream = await fetch(`${contentApiUrl()}/api/${path}${url.search}`, {
      method: request.method,
      headers,
      body: ['GET', 'HEAD'].includes(request.method) ? undefined : await request.arrayBuffer(),
      redirect: 'manual',
      signal: AbortSignal.timeout(10000),
    });
    if (upstream.status >= 300 && upstream.status < 400) return authResponse('Unexpected API redirect.', 502);
    if (upstream.status >= 500) return authResponse('Content service unavailable.', 502);
    if (upstream.status === 401) cookies.delete(SESSION_COOKIE, { path: '/' });
    return new Response(upstream.body, {
      status: upstream.status,
      headers: { 'Content-Type': upstream.headers.get('content-type') || 'application/json', 'Cache-Control': 'no-store' },
    });
  } catch {
    return authResponse('Content service unavailable. Please try again.', 503);
  }
};
