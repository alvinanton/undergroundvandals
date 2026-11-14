import type { APIRoute } from 'astro';
import { authResponse, SESSION_COOKIE } from '../../../lib/server/auth';

export const POST: APIRoute = ({ cookies }) => {
  cookies.delete(SESSION_COOKIE, { path: '/' });
  return authResponse('Signed out.', 200);
};
