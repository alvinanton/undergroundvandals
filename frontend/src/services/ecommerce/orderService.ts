// src/services/ecommerce/orderService.ts
import type {
  CheckoutRequest,
  CheckoutResponse,
  ShippingPreviewRequest,
  ShippingPreviewResponse,
  OrderView,
  ApiProblemDetails,
} from '../../types/ecommerce';

const API_URL = import.meta.env.PUBLIC_ECOMMERCE_API_URL || 'http://localhost:5098';

export type OrderServiceResult<T> =
  | { success: true; data: T }
  | { success: false; problem: ApiProblemDetails };

async function parseResponse<T>(response: Response): Promise<OrderServiceResult<T>> {
  if (response.ok) {
    const data = response.status === 204 ? (undefined as T) : await response.json();
    return { success: true, data };
  }

  let problem: ApiProblemDetails;
  try {
    problem = await response.json();
  } catch {
    problem = { type: '', title: 'UnknownError', status: response.status, traceId: '' };
  }
  return { success: false, problem };
}

/** POST /api/orders/checkout — public, no auth. Throws only on network failure. */
export async function checkout(request: CheckoutRequest): Promise<OrderServiceResult<CheckoutResponse>> {
  const response = await fetch(`${API_URL}/api/orders/checkout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return parseResponse<CheckoutResponse>(response);
}

/** POST /api/orders/shipping-preview — public, creates nothing. */
export async function previewShipping(
  request: ShippingPreviewRequest,
): Promise<OrderServiceResult<ShippingPreviewResponse>> {
  const response = await fetch(`${API_URL}/api/orders/shipping-preview`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  return parseResponse<ShippingPreviewResponse>(response);
}

/** GET /api/orders/{orderNumber} — requires the email used at checkout. */
export async function getOrder(
  orderNumber: string,
  customerEmail: string,
): Promise<OrderServiceResult<OrderView>> {
  const response = await fetch(`${API_URL}/api/orders/${encodeURIComponent(orderNumber)}`, {
    headers: { 'X-Customer-Email': customerEmail },
  });
  return parseResponse<OrderView>(response);
}