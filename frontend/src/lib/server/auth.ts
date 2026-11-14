import type { AstroCookies } from 'astro';

export const SESSION_COOKIE = 'uv_auth_token';

export interface SessionUser {
  id: string;
  username: string;
  email: string;
  role: 'Admin' | 'Editor';
  expiresAt: number;
}

export function contentApiUrl(): string {
  return (process.env.API_INTERNAL_URL || import.meta.env.API_INTERNAL_URL ||
    import.meta.env.PUBLIC_API_URL || 'http://localhost:5098').replace(/\/$/, '');
}

export function siteOrigin(url: URL): string {
  const configured = process.env.SITE_ORIGIN || import.meta.env.SITE_ORIGIN;
  const origin = configured ? new URL(configured) : url;
  if (!['http:', 'https:'].includes(origin.protocol)) throw new Error('Invalid site origin.');
  return origin.origin;
}

export async function validateSession(token: string): Promise<SessionUser | null> {
  if (!token || token.length > 4096) return null;
  const response = await fetch(`${contentApiUrl()}/api/auth/session`, {
    headers: { Authorization: `Bearer ${token}` },
    redirect: 'manual',
    signal: AbortSignal.timeout(10000),
  });
  if (response.status === 401 || response.status === 403) return null;
  if (!response.ok) throw new Error('Session validation unavailable.');
  const user = await response.json();
  if (!user || typeof user.id !== 'string' || !user.id ||
      typeof user.username !== 'string' || typeof user.email !== 'string' ||
      !['Admin', 'Editor'].includes(user.role) ||
      !Number.isSafeInteger(user.expiresAt) || user.expiresAt <= Math.floor(Date.now() / 1000)) return null;
  return user;
}

export function setSessionCookie(cookies: AstroCookies, url: URL, token: string, expiresAt: number): void {
  cookies.set(SESSION_COOKIE, token, {
    path: '/',
    httpOnly: true,
    secure: new URL(siteOrigin(url)).protocol === 'https:',
    sameSite: 'lax',
    maxAge: Math.max(0, Math.min(28800, expiresAt - Math.floor(Date.now() / 1000))),
  });
}

export function authResponse(message: string, status: number): Response {
  return Response.json({ message }, { status, headers: { 'Cache-Control': 'no-store' } });
}
