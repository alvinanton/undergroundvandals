// src/types/ecommerce.ts
//
// Mirrors the C# DTOs in ECommerceApp.Application / ECommerceApp.API.
// Keep this in sync manually whenever a DTO changes on the backend —
// there is no shared schema generation between the two projects yet.

// ─────────────────────────────────────────────────────────────────────────
// Enums
// ─────────────────────────────────────────────────────────────────────────

/**
 * Sent as a NUMBER in requests (ASP.NET default enum serialization):
 *   BankTransfer = 0, CashOnDelivery = 1, Card = 2
 * Received as a STRING in responses (the API calls .ToString() explicitly
 * in OrdersController, e.g. "BankTransfer").
 * CashOnDelivery is accepted by this type but the API currently REJECTS it
 * at checkout (PaymentMethodNotSupported) until delivery logistics exist.
 */
export const PaymentMethod = {
  BankTransfer: 0,
  CashOnDelivery: 1,
  Card: 2,
} as const;
export type PaymentMethodValue = (typeof PaymentMethod)[keyof typeof PaymentMethod];

/** String values as returned by GET /api/orders/{orderNumber}. */
export type PaymentMethodName = "BankTransfer" | "CashOnDelivery" | "Card";

/** Order.Status, always returned as a string (HasConversion<string> + .ToString()). */
export type OrderStatus =
  | "AwaitingShippingQuote"
  | "PendingPayment"
  | "Paid"
  | "Processing"
  | "Shipped"
  | "Completed"
  | "Cancelled"
  | "Expired"
  | "Refunded";

// ─────────────────────────────────────────────────────────────────────────
// Products
// ─────────────────────────────────────────────────────────────────────────

/** GET /api/product, /search, /{id}, /category/{id} — public, no FileUrl/FileSize. */
export interface ProductPublic {
  id: number;
  name: string;
  description: string;
  shortDescription: string;
  price: number;
  categoryId: number;
  isDigital: boolean;
  thumbnailUrl: string | null;
  createdAt: string; // ISO 8601
}

/** Response of POST/PUT /api/product — admin only, includes FileUrl/FileSize/stock. */
export interface ProductAdmin {
  id: number;
  name: string;
  description: string;
  shortDescription: string;
  price: number;
  categoryId: number;
  isDigital: boolean;
  fileUrl: string | null;
  fileSize: number | null;
  stockQuantity: number | null; // null for digital products
  thumbnailUrl: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

/** Body for POST /api/product (admin, X-Api-Key required). */
export interface CreateProductRequest {
  name: string;
  description: string;
  shortDescription: string;
  price: number;
  categoryId: number;
  isDigital: boolean;
  fileUrl?: string | null;
  fileSize?: number | null;
  stockQuantity?: number | null; // required by the API when isDigital is false
  thumbnailUrl?: string | null;
  isActive: boolean;
}

/** Body for PUT /api/product/{id}. PUT replaces the whole product — send every field. */
export type UpdateProductRequest = CreateProductRequest;

// ─────────────────────────────────────────────────────────────────────────
// Categories
// ─────────────────────────────────────────────────────────────────────────

/**
 * GET /api/categories, /{id}, /{id}/subcategories.
 * CategoriesController still returns the entity directly (not yet split into
 * public/admin DTOs like Product), so this may include navigation fields
 * depending on what the serializer resolves. Treat extra fields as optional.
 */
export interface Category {
  id: number;
  name: string;
  description: string | null;
  parentCategoryId: number | null;
  isActive: boolean;
}

// ─────────────────────────────────────────────────────────────────────────
// Checkout
// ─────────────────────────────────────────────────────────────────────────

export interface CheckoutItemRequest {
  productId: number;
  quantity: number;
}

/** Body for POST /api/orders/checkout — public, no auth required. */
export interface CheckoutRequest {
  customerEmail: string;
  customerName: string;
  customerPhone?: string | null;
  /** Required by the API only when the cart contains at least one physical product. */
  shippingAddress?: string | null;
  paymentMethod: PaymentMethodValue;
  items: CheckoutItemRequest[];
}

/** 201 response body from POST /api/orders/checkout. */
export interface CheckoutResponse {
  orderNumber: string;
  totalAmount: number;
}

// ─────────────────────────────────────────────────────────────────────────
// Order lookup (customer-facing)
// ─────────────────────────────────────────────────────────────────────────

export interface OrderItemView {
  productName: string;
  quantity: number;
  unitPrice: number;
  subtotal: number;
}

/**
 * GET /api/orders/{orderNumber} — requires header "X-Customer-Email" matching
 * the email used at checkout. Returns 404 for a wrong number OR a wrong email
 * (never distinguishes which, to avoid order enumeration).
 */
export interface OrderView {
  orderNumber: string;
  status: OrderStatus;
  paymentMethod: PaymentMethodName; // string here, unlike the checkout request
  currency: string; // "GTQ"
  subtotal: number;
  shippingCost: number;
  totalAmount: number;
  shippingCarrier: string | null;
  expiresAt: string | null; // ISO 8601, null once paid/completed
  createdAt: string;
  items: OrderItemView[];
}

// ─────────────────────────────────────────────────────────────────────────
// Shipping preview (no order is created)
// ─────────────────────────────────────────────────────────────────────────

/** Body for POST /api/orders/shipping-preview — public. */
export interface ShippingPreviewRequest {
  shippingAddress?: string | null;
  items: CheckoutItemRequest[];
}

export interface ShippingPreviewResponse {
  subtotal: number;
  /** null when the provider is in Manual mode and has not quoted yet. */
  shippingCost: number | null;
  carrier: string | null;
  /** null when shippingCost is null (price not confirmed yet). */
  totalAmount: number | null;
  requiresShipping: boolean;
  /** false in Manual mode: the price shown is not final, checkout will still succeed. */
  priceConfirmed: boolean;
  detail: string | null;
}

// ─────────────────────────────────────────────────────────────────────────
// Payment confirmation (admin only)
// ─────────────────────────────────────────────────────────────────────────

/** Body for POST /api/orders/{orderNumber}/confirm-payment — requires X-Api-Key. */
export interface ConfirmPaymentRequest {
  paymentReference: string;
}

// ─────────────────────────────────────────────────────────────────────────
// Error shape (RFC 9457 ProblemDetails, used by every endpoint in this API)
// ─────────────────────────────────────────────────────────────────────────

export interface ApiProblemDetails {
  type: string;
  /** The CheckoutError / ConfirmPaymentError enum name, e.g. "InsufficientStock". */
  title: string;
  status: number;
  detail?: string;
  traceId: string;
  /** Present only on 400s from DataAnnotations model validation (Create/UpdateProductDto). */
  errors?: Record<string, string[]>;
}

/** Narrows an unknown fetch error body into ApiProblemDetails. */
export function isApiProblemDetails(value: unknown): value is ApiProblemDetails {
  return (
    typeof value === "object" &&
    value !== null &&
    "title" in value &&
    "status" in value
  );
}