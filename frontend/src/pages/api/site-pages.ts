import type { APIRoute } from 'astro';
import { contentApiUrl } from '../../lib/server/auth';

export const GET: APIRoute = async () => {
  try {
    const response = await fetch(`${contentApiUrl()}/api/site-pages`, {
      signal: AbortSignal.timeout(8000),
    });
    return new Response(response.body, {
      status: response.status,
      headers: { 'Content-Type': response.headers.get('content-type') || 'application/json', 'Cache-Control': 'no-store' },
    });
  } catch {
    return Response.json({ message: 'Page settings are temporarily unavailable.' }, { status: 503 });
  }
};
