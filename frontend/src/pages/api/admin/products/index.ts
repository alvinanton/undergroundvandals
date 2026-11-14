import type { APIRoute } from 'astro';

export const GET: APIRoute = async ({ request }) => {
  const url = new URL(request.url);
  
  // Nombre exacto según tu archivo .env
  const apiKey = import.meta.env.ECOMMERCE_ADMIN_API_KEY || '';
  const baseUrl = import.meta.env.ECOMMERCE_ADMIN_API_URL || 'http://localhost:5114';

  console.log('[Astro Proxy] Clave detectada:', apiKey ? `${apiKey.substring(0, 4)}***` : 'VACÍA / UNDEFINED');

  try {
    const backendUrl = `${baseUrl}/api/product/admin${url.search}`;

    const response = await fetch(backendUrl, {
      method: 'GET',
      headers: {
        'X-Api-Key': apiKey,
        'Content-Type': 'application/json'
      }
    });

    const text = await response.text();

    if (!response.ok) {
      console.error(`[.NET API Error ${response.status}]:`, text);
      return new Response(
        JSON.stringify({
          items: [],
          pageNumber: 1,
          pageSize: 25,
          totalItems: 0,
          totalPages: 1,
          error: `Backend HTTP ${response.status}: ${text || 'Check API Key configuration'}`
        }),
        {
          status: response.status,
          headers: { 'Content-Type': 'application/json' }
        }
      );
    }

    const data = text.trim() ? JSON.parse(text) : { items: [], pageNumber: 1, pageSize: 25, totalItems: 0, totalPages: 1 };

    return new Response(JSON.stringify(data), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    });
  } catch (error: any) {
    console.error('Error connecting to .NET API:', error);
    return new Response(
      JSON.stringify({
        items: [],
        pageNumber: 1,
        pageSize: 25,
        totalItems: 0,
        totalPages: 1,
        error: error.message || 'Unable to connect to .NET backend.'
      }),
      {
        status: 500,
        headers: { 'Content-Type': 'application/json' }
      }
    );
  }
};