import type { APIRoute } from 'astro';

export const GET: APIRoute = async ({ request }) => {
  const url = new URL(request.url);
  const baseUrl = import.meta.env.PUBLIC_API_URL || 'http://localhost:5114';

  try {
    const response = await fetch(`${baseUrl}/api/gallery${url.search}`);
    const data = await response.json();

    return new Response(JSON.stringify(data), {
      status: response.status,
      headers: { 'Content-Type': 'application/json' }
    });
  } catch (error: any) {
    return new Response(
      JSON.stringify({ error: error.message || 'Error connecting to backend API' }),
      { status: 500, headers: { 'Content-Type': 'application/json' } }
    );
  }
};