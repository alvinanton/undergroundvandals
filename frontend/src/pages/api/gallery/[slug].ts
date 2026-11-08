import type { APIRoute } from 'astro';

export const GET: APIRoute = async ({ params }) => {
  const { slug } = params;
  const baseUrl = import.meta.env.PUBLIC_API_URL || 'http://localhost:5114';

  if (!slug) {
    return new Response(JSON.stringify({ error: 'Slug is required' }), { status: 400 });
  }

  try {
    const response = await fetch(`${baseUrl}/api/gallery/slug/${slug}`);
    
    if (!response.ok) {
      return new Response(JSON.stringify({ error: 'Article not found' }), { status: response.status });
    }

    const data = await response.json();

    return new Response(JSON.stringify(data), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    });
  } catch (error: any) {
    return new Response(
      JSON.stringify({ error: error.message || 'Error connecting to backend API' }),
      { status: 500, headers: { 'Content-Type': 'application/json' } }
    );
  }
};