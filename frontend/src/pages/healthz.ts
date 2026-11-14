import type { APIRoute } from 'astro';

export const GET: APIRoute = () => Response.json(
  { status: 'healthy' },
  { headers: { 'Cache-Control': 'no-store' } },
);
