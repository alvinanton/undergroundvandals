import type { APIRoute } from 'astro';
import { authResponse, contentApiUrl, setSessionCookie, validateSession } from '../../../lib/server/auth';

export const POST: APIRoute = async ({ request, cookies, url }) => {
  let credentials;
  try {
    credentials = await request.json();
  } catch {
    return authResponse('Invalid login request.', 400);
  }
  if (!credentials || typeof credentials.email !== 'string' || typeof credentials.password !== 'string' ||
      !credentials.email || !credentials.password || credentials.email.length > 254 || credentials.password.length > 1024) {
    return authResponse('Email and password are required.', 400);
  }
  try {
    const response = await fetch(`${contentApiUrl()}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: credentials.email, password: credentials.password }),
      redirect: 'manual',
      signal: AbortSignal.timeout(10000),
    });
    if (response.status === 401) return authResponse('Invalid credentials or inactive account.', 401);
    if (!response.ok) return authResponse('Authentication service unavailable.', 502);
    const data = await response.json();
    if (typeof data.token !== 'string') return authResponse('Invalid authentication response.', 502);
    const user = await validateSession(data.token);
    if (!user) return authResponse('Invalid authentication response.', 502);
    setSessionCookie(cookies, url, data.token, user.expiresAt);
    return authResponse('Signed in.', 200);
  } catch {
    return authResponse('Authentication service unavailable. Please try again.', 503);
  }
};
