// src/lib/ecommerceStorage.ts
import { compressImageToWebP } from "./imageOptimizer";

export interface StorageUploadResult {
  fileUrl: string;
  storageKey: string;
}

export interface UploadUrlResponse {
  uploadUrl: string;
  storageKey: string;
  fileUrl: string;
}

export interface ChunkSessionResponse {
  uploadId: string;
  storageKey: string;
  recommendedChunkSizeBytes: number;
  baseUploadUrl: string;
}

const CHUNK_THRESHOLD = 8 * 1024 * 1024; // 8 MB

/**
 * Uploads a file directly to Azure Blob Storage through the E-commerce backend API.
 * Compresses images to WebP and switches between single-blob upload and chunked block upload based on size.
 */
export async function uploadEcommerceMedia(
  file: File,
  categorySlug: string = "products",
  isPrivate: boolean = false,
  apiKey: string = import.meta.env.PUBLIC_ECOMMERCE_ADMIN_API_KEY || "",
  apiUrl: string = import.meta.env.PUBLIC_ECOMMERCE_ADMIN_API_URL || "http://localhost:5114",
  onProgress?: (percentage: number) => void
): Promise<StorageUploadResult> {
  // 1. Client-side optimization if the asset is an image
  let fileToUpload = file;
  if (file.type.startsWith("image/")) {
    fileToUpload = await compressImageToWebP(file);
  }

  // 2. Choose upload strategy based on processed file size
  if (fileToUpload.size <= CHUNK_THRESHOLD) {
    return uploadSingleBlob(fileToUpload, categorySlug, isPrivate, apiKey, apiUrl, onProgress);
  } else {
    return uploadInChunks(fileToUpload, categorySlug, isPrivate, apiKey, apiUrl, onProgress);
  }
}

/**
 * Strategy 1: Direct single-blob upload for assets <= 8 MB
 */
async function uploadSingleBlob(
  file: File,
  categorySlug: string,
  isPrivate: boolean,
  apiKey: string,
  apiUrl: string,
  onProgress?: (percentage: number) => void
): Promise<StorageUploadResult> {
  // Request SAS upload URL from the backend
  const res = await fetch(`${apiUrl}/api/media/upload-url`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Api-Key": apiKey
    },
    body: JSON.stringify({
      fileName: file.name,
      contentType: file.type || "application/octet-stream",
      categorySlug,
      isPrivate
    })
  });

  if (!res.ok) {
    throw new Error(`Failed to obtain upload authorization URL (Status ${res.status})`);
  }

  const sasData: UploadUrlResponse = await res.json();

  // Upload directly to Azure Blob Storage
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", sasData.uploadUrl, true);
    xhr.setRequestHeader("x-ms-blob-type", "BlockBlob");
    xhr.setRequestHeader("Content-Type", file.type || "application/octet-stream");

    if (onProgress) {
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable) {
          onProgress(Math.round((event.loaded / event.total) * 100));
        }
      };
    }

    xhr.onload = () => {
      if (xhr.status === 200 || xhr.status === 201) {
        resolve({ fileUrl: sasData.fileUrl, storageKey: sasData.storageKey });
      } else {
        reject(new Error(`Failed to upload file to Azure Blob Storage (Status ${xhr.status})`));
      }
    };

    xhr.onerror = () => reject(new Error("Network error during direct upload to Azure Storage."));
    xhr.send(file);
  });
}

/**
 * Strategy 2: Chunked block upload for large assets (> 8 MB)
 */
async function uploadInChunks(
  file: File,
  categorySlug: string,
  isPrivate: boolean,
  apiKey: string,
  apiUrl: string,
  onProgress?: (percentage: number) => void
): Promise<StorageUploadResult> {
  // 1. Initialize chunked upload session in the backend
  const initRes = await fetch(`${apiUrl}/api/media/chunk-session`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Api-Key": apiKey
    },
    body: JSON.stringify({
      fileName: file.name,
      contentType: file.type || "application/octet-stream",
      categorySlug,
      totalSizeBytes: file.size,
      isPrivate
    })
  });

  if (!initRes.ok) {
    throw new Error(`Failed to initialize chunked upload session (Status ${initRes.status})`);
  }

  const session: ChunkSessionResponse = await initRes.json();
  const chunkSize = session.recommendedChunkSizeBytes || CHUNK_THRESHOLD;
  const totalChunks = Math.ceil(file.size / chunkSize);
  const blockIds: string[] = [];

  // 2. Upload each block directly to Azure
  for (let i = 0; i < totalChunks; i++) {
    const start = i * chunkSize;
    const end = Math.min(start + chunkSize, file.size);
    const chunk = file.slice(start, end);

    // Consistent Base64 ID (e.g. "block-000000")
    const blockId = btoa(`block-${String(i).padStart(6, "0")}`);
    blockIds.push(blockId);

    const separator = session.baseUploadUrl.includes("?") ? "&" : "?";
    const blockUrl = `${session.baseUploadUrl}${separator}comp=block&blockid=${encodeURIComponent(blockId)}`;

    await uploadChunk(chunk, blockUrl);

    if (onProgress) {
      onProgress(Math.round(((i + 1) / totalChunks) * 100));
    }
  }

  // 3. Commit uploaded blocks through the backend endpoint
  const commitRes = await fetch(`${apiUrl}/api/media/commit-chunk`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Api-Key": apiKey
    },
    body: JSON.stringify({
      storageKey: session.storageKey,
      isPrivate,
      blockIds
    })
  });

  if (!commitRes.ok) {
    throw new Error(`Failed to commit block list on backend API (Status ${commitRes.status})`);
  }

  const commitData = await commitRes.json();
  return {
    fileUrl: commitData.fileUrl,
    storageKey: commitData.storageKey
  };
}

/**
 * Uploads a single chunk block via HTTP PUT
 */
function uploadChunk(chunk: Blob, blockUrl: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", blockUrl, true);

    xhr.onload = () => {
      if (xhr.status === 200 || xhr.status === 201) {
        resolve();
      } else {
        reject(new Error(`Failed to upload block chunk (Status ${xhr.status})`));
      }
    };

    xhr.onerror = () => reject(new Error("Network error during block chunk upload."));
    xhr.send(chunk);
  });
}