declare var process: any;

if (typeof process !== 'undefined') {
  process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
}

import type { MediaItem } from '../types/media';

// Asegúrate de que el puerto (5098 o el que te dé VS) coincida con tu API
const API_URL = import.meta.env.PUBLIC_API_URL || 'http://localhost:5098';

/**
 * Fetches media items from the .NET backend with optional category & tag filters.
 */
export async function getMediaItems(category?: string, tag?: string): Promise<MediaItem[]> {
  try {
    const params = new URLSearchParams();
    
    if (category) params.append('category', category);
    if (tag) params.append('tag', tag);

    const queryString = params.toString();
    const url = `${API_URL}/api/media${queryString ? `?${queryString}` : ''}`;

    const response = await fetch(url);

    if (!response.ok) {
      throw new Error(`Request failed: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Error fetching media from API:', error);
    return [];
  }
}