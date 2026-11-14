// src/services/ecommerce/categoryService.ts
import type { Category } from '../../types/ecommerce';

const API_URL = import.meta.env.PUBLIC_ECOMMERCE_API_URL || 'http://localhost:5114';

export async function getCategories(): Promise<Category[]> {
  try {
    const response = await fetch(`${API_URL}/api/categories`);
    if (!response.ok) throw new Error(`Request failed: ${response.statusText}`);
    return await response.json();
  } catch (error) {
    console.error('Error fetching categories from API:', error);
    return [];
  }
}