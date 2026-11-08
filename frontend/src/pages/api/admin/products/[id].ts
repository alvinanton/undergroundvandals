// src/pages/api/admin/products/[id].ts
// Same proxy pattern as index.ts — see that file for the auth/key rationale.

import type { APIRoute } from 'astro';

const API_URL = import.meta.env.ECOMMERCE_ADMIN_API_URL || 'http://localhost:5098';
const API_KEY = import.meta.env.ECOMMERCE_ADMIN_API_KEY;

interface LocalsUser {
  role: string;
  email: string;
}

function requireAdmin(locals: App.Locals): Response | null {
  const user = (locals as { user?: LocalsUser | null }).user;
  if (user?.role !== 'Admin') {
    return new Response(JSON.stringify({ message: 'Forbidden: admin role required.' }), {
      status: 403,
      headers: { 'Content-Type': 'application/json' },
    });
  }
  return null;
}

function missingKeyResponse(): Response {
  console.error('ECOMMERCE_ADMIN_API_KEY is not set.');
  return new Response(JSON.stringify({ message: 'Server misconfigured: missing admin API key.' }), {
    status: 500,
    headers: { 'Content-Type': 'application/json' },
  });
}

function badId(): Response {
  return new Response(JSON.stringify({ message: 'Invalid product id.' }), {
    status: 400,
    headers: { 'Content-Type': 'application/json' },
  });
}

export const GET: APIRoute = async ({ locals, params }) => {
  const forbidden = requireAdmin(locals);
  if (forbidden) return forbidden;
  if (!API_KEY) return missingKeyResponse();

  const id = Number(params.id);
  if (!Number.isInteger(id) || id <= 0) return badId();

  try {
    const response = await fetch(`${API_URL}/api/product/admin/${id}`, {
      headers: { 'X-Api-Key': API_KEY },
    });

    const body = await response.text();
    return new Response(body, {
      status: response.status,
      headers: { 'Content-Type': 'application/json' },
    });
  } catch (error) {
    console.error(`Error proxying GET /api/product/admin/${id}:`, error);
    return new Response(JSON.stringify({ message: 'Could not reach the e-commerce API.' }), {
      status: 502,
      headers: { 'Content-Type': 'application/json' },
    });
  }
};

export const PUT: APIRoute = async ({ locals, params, request }) => {
  const forbidden = requireAdmin(locals);
  if (forbidden) return forbidden;
  if (!API_KEY) return missingKeyResponse();

  const id = Number(params.id);
  if (!Number.isInteger(id) || id <= 0) return badId();

  try {
    const payload = await request.text();

    const response = await fetch(`${API_URL}/api/product/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        'X-Api-Key': API_KEY,
      },
      body: payload,
    });

    const body = await response.text();
    return new Response(body, {
      status: response.status,
      headers: { 'Content-Type': 'application/json' },
    });
  } catch (error) {
    console.error(`Error proxying PUT /api/product/${id}:`, error);
    return new Response(JSON.stringify({ message: 'Could not reach the e-commerce API.' }), {
      status: 502,
      headers: { 'Content-Type': 'application/json' },
    });
  }
};