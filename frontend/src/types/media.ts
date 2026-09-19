export interface MediaAsset {
  url: string;
  type: 'image' | 'video';
}

export interface MediaItem {
  id: string | number;
  description: string;
  category?: string;
  hashtags: string[];
  media: MediaAsset[]; 
  createdAt?: string;
}