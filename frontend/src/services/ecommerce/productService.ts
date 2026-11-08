// src/services/ecommerce/productService.ts
import type { ProductPublic, ApiProblemDetails } from '../../types/ecommerce';

const API_URL = import.meta.env.PUBLIC_ECOMMERCE_API_URL || 'http://localhost:5114';

export async function getProducts(categoryId?: number): Promise<ProductPublic[]> {
  try {
    const url = categoryId
      ? `${API_URL}/api/product/category/${categoryId}`
      : `${API_URL}/api/product`;

    const response = await fetch(url);

    if (response.status === 404) return []; // category/search with no matches
    if (!response.ok) {
      throw new Error(`Request failed: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Error fetching products from API:', error);
    return [];
  }
}

export async function getProductById(id: number): Promise<ProductPublic | null> {
  try {
    const response = await fetch(`${API_URL}/api/product/${id}`);
    if (response.status === 404) return null;
    if (!response.ok) throw new Error(`Request failed: ${response.statusText}`);
    return await response.json();
  } catch (error) {
    console.error('Error fetching product from API:', error);
    return null;
  }
}