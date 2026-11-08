// src/lib/cart.ts
//
// Client-only cart state, stored in localStorage. Deliberately minimal: only
// productId + quantity are kept. Price, name, stock, etc. are never trusted
// from here — the checkout endpoint recalculates everything against the
// database, the same way it already does for the rest of the API.
//
// Import this ONLY from <script> tags (client bundle), never from Astro
// frontmatter (SSR / Node, where localStorage doesn't exist).

export interface CartItem {
  productId: number;
  quantity: number;
}

const STORAGE_KEY = 'uv_cart';

/** Mirrors the backend's OrderService limits, just to keep the UI sane. */
const MAX_QUANTITY_PER_LINE = 99;

/** Fired on window whenever the cart changes, so any page/component can react live. */
export const CART_UPDATED_EVENT = 'cart:updated';

function isBrowser(): boolean {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

function clampQuantity(quantity: number): number {
  return Math.max(1, Math.min(MAX_QUANTITY_PER_LINE, Math.floor(quantity)));
}

export function getCart(): CartItem[] {
  if (!isBrowser()) return [];

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];

    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];

    return parsed.filter(
      (item): item is CartItem =>
        typeof item?.productId === 'number' &&
        typeof item?.quantity === 'number' &&
        item.quantity > 0,
    );
  } catch {
    // Corrupted or manually edited localStorage: treat as an empty cart rather than crash.
    return [];
  }
}

function persist(items: CartItem[]): void {
  if (!isBrowser()) return;

  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  window.dispatchEvent(new CustomEvent(CART_UPDATED_EVENT, { detail: { items } }));
}

/** Adds a product, or increases its quantity if it's already in the cart. */
export function addToCart(productId: number, quantity: number = 1): CartItem[] {
  const cart = getCart();
  const existing = cart.find((i) => i.productId === productId);

  if (existing) {
    existing.quantity = clampQuantity(existing.quantity + quantity);
  } else {
    cart.push({ productId, quantity: clampQuantity(quantity) });
  }

  persist(cart);
  return cart;
}

/** Sets an exact quantity. A quantity of 0 or less removes the item. */
export function updateQuantity(productId: number, quantity: number): CartItem[] {
  let cart = getCart();

  if (quantity <= 0) {
    cart = cart.filter((i) => i.productId !== productId);
  } else {
    const existing = cart.find((i) => i.productId === productId);
    if (existing) existing.quantity = clampQuantity(quantity);
  }

  persist(cart);
  return cart;
}

export function removeFromCart(productId: number): CartItem[] {
  return updateQuantity(productId, 0);
}

export function clearCart(): void {
  persist([]);
}

/** Total units across all lines, e.g. for the header badge. */
export function getCartCount(): number {
  return getCart().reduce((sum, item) => sum + item.quantity, 0);
}