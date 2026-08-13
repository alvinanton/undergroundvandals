export enum MediaType {
  Photo = 0,
  Video = 1
}

export interface MediaItem {
  id: string;
  title: string;
  description?: string;
  type: MediaType;
  url: string;
  category: string;
  hashtags: string[];   
  isArchived: boolean;
  createdAt: string;
}