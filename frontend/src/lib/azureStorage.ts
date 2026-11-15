// src/lib/azureStorage.ts
import { compressImageToWebP } from "./imageOptimizer";

export interface AzureUploadResult {
  url: string;
  publicId: string;
}

export interface SasResponse {
  uploadUrl: string;
  fileUrl: string;
  publicId: string;
  provider?: string;
  uncommittedBlockIds?: string[];
}

interface StoredUploadSession {
  uploadSessionId: string;
  fileSize: number;
  lastModified: number;
  fileType: string;
}

const CHUNK_SIZE = 8 * 1024 * 1024; // 8 MB por trozo
const MAX_CONCURRENT_CHUNKS = 4;
const MAX_CHUNK_RETRIES = 3;
const MAX_BLOCK_COUNT = 50_000;

/**
 * Uploads a file directly to Azure Blob Storage using a SAS URL.
 * Automatically optimizes images to WebP and switches between single-blob
 * upload and chunked block upload based on processed file size.
 */
export async function uploadFileToAzure(
  file: File,
  targetPath: string,
  onProgress?: (percentage: number) => void
): Promise<AzureUploadResult> {
  // 0. Optimización en cliente: Si es imagen, comprimir a WebP transparente en navegador
  let fileToUpload = file;
  if (file.type.startsWith("image/")) {
    fileToUpload = await compressImageToWebP(file);
  }

  // 2. Elegir estrategia según el tamaño del archivo procesado
  if (fileToUpload.size <= CHUNK_SIZE) {
    const sasData = await requestUploadSignature(targetPath);
    return uploadSingleBlob(fileToUpload, sasData, onProgress);
  } else {
    return uploadBlobInChunks(file, fileToUpload, targetPath, onProgress);
  }
}

async function requestUploadSignature(targetPath: string, uploadSessionId?: string): Promise<SasResponse> {
  const query = new URLSearchParams({ target: targetPath });
  if (uploadSessionId) query.set("uploadSessionId", uploadSessionId);

  const response = await fetch(`/api/admin/content/media/upload-signature?${query}`, { method: "GET" });
  if (!response.ok) {
    throw new Error("Failed to obtain upload authorization for Azure Blob Storage.");
  }
  return response.json();
}

function getUploadSessionKey(file: File, targetPath: string): string {
  return `uv.azure-upload:${encodeURIComponent(targetPath)}:${encodeURIComponent(file.name)}`;
}

function getOrCreateUploadSession(file: File, targetPath: string): { key: string; id: string } {
  const key = getUploadSessionKey(file, targetPath);
  try {
    const stored = localStorage.getItem(key);
    if (stored) {
      const session: StoredUploadSession = JSON.parse(stored);
      if (session.fileSize === file.size && session.lastModified === file.lastModified && session.fileType === file.type) {
        return { key, id: session.uploadSessionId };
      }
    }
  } catch {
    // Storage may be unavailable or contain invalid data; start a fresh session.
  }

  const id = crypto.randomUUID();
  const session: StoredUploadSession = {
    uploadSessionId: id,
    fileSize: file.size,
    lastModified: file.lastModified,
    fileType: file.type,
  };
  try {
    localStorage.setItem(key, JSON.stringify(session));
  } catch {
    // The current upload still works, but cannot resume after a page reload.
  }
  return { key, id };
}

/**
 * Subida estándar para archivos pequeños (< 8MB)
 */
function uploadSingleBlob(
  file: File,
  sasData: SasResponse,
  onProgress?: (percentage: number) => void
): Promise<AzureUploadResult> {
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
        resolve({ url: sasData.fileUrl, publicId: sasData.publicId });
      } else {
        reject(new Error(`Failed to store file in Azure (Status ${xhr.status})`));
      }
    };

    xhr.onerror = () => reject(new Error("Network error during direct upload to Azure."));
    xhr.send(file);
  });
}

/**
 * Subida por trozos para archivos grandes (>= 8MB)
 */
async function uploadBlobInChunks(
  originalFile: File,
  file: File,
  targetPath: string,
  onProgress?: (percentage: number) => void
): Promise<AzureUploadResult> {
  const totalChunks = Math.ceil(file.size / CHUNK_SIZE);
  if (totalChunks > MAX_BLOCK_COUNT) {
    throw new Error("This file exceeds Azure Blob Storage's maximum block count.");
  }

  const session = getOrCreateUploadSession(originalFile, targetPath);
  let sasData = await requestUploadSignature(targetPath, session.id);
  let refreshInFlight: Promise<SasResponse> | null = null;
  const refreshAuthorization = async (rejectedUrl: string) => {
    if (sasData.uploadUrl !== rejectedUrl) return;
    if (!refreshInFlight) {
      refreshInFlight = requestUploadSignature(targetPath, session.id);
    }
    try {
      sasData = await refreshInFlight;
    } finally {
      refreshInFlight = null;
    }
  };
  const alreadyUploaded = new Set(sasData.uncommittedBlockIds ?? []);
  const chunks: Array<{ blockId: string; chunk: Blob }> = [];
  const blockIds = new Array<string>(totalChunks);
  let completedBytes = 0;

  for (let i = 0; i < totalChunks; i++) {
    const start = i * CHUNK_SIZE;
    const end = Math.min(start + CHUNK_SIZE, file.size);
    const chunk = file.slice(start, end);

    const blockId = await createContentBlockId(i, chunk);
    blockIds[i] = blockId;
    if (alreadyUploaded.has(blockId)) {
      completedBytes += chunk.size;
    } else {
      chunks.push({ blockId, chunk });
    }
  }
  onProgress?.(Math.round((completedBytes / file.size) * 100));

  let nextChunkIndex = 0;
  const uploadWorker = async () => {
    while (nextChunkIndex < chunks.length) {
      const chunkIndex = nextChunkIndex++;
      const { blockId, chunk } = chunks[chunkIndex];
      const getBlockUrl = () => {
        const separator = sasData.uploadUrl.includes("?") ? "&" : "?";
        return `${sasData.uploadUrl}${separator}comp=block&blockid=${encodeURIComponent(blockId)}`;
      };
      await uploadChunkWithRetry(chunk, getBlockUrl, () => sasData.uploadUrl, refreshAuthorization);
      completedBytes += chunk.size;
      onProgress?.(Math.round((completedBytes / file.size) * 100));
    }
  };

  await Promise.all(
    Array.from(
      { length: Math.min(MAX_CONCURRENT_CHUNKS, chunks.length) },
      () => uploadWorker()
    )
  );

  // Confirmar y ensamblar la lista de bloques en Azure
  await commitBlockListWithRetry(
    () => sasData.uploadUrl,
    blockIds,
    file.type,
    refreshAuthorization
  );

  try {
    localStorage.removeItem(session.key);
  } catch {
    // A stale local session is harmless; Azure has committed the completed blob.
  }

  return {
    url: sasData.fileUrl,
    publicId: sasData.publicId,
  };
}

async function createContentBlockId(index: number, chunk: Blob): Promise<string> {
  const data = await chunk.arrayBuffer();
  const prefix = new TextEncoder().encode(String(index).padStart(6, "0"));
  const input = new Uint8Array(prefix.length + data.byteLength);
  input.set(prefix);
  input.set(new Uint8Array(data), prefix.length);
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", input));
  let binary = "";
  for (const byte of digest) binary += String.fromCharCode(byte);
  return btoa(binary);
}

/**
 * Envía un trozo individual mediante PUT
 */
function uploadChunk(chunk: Blob, blockUrl: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", blockUrl, true);
    xhr.timeout = 120_000;

    xhr.onload = () => {
      if (xhr.status === 200 || xhr.status === 201) {
        resolve();
      } else {
        reject(new ChunkUploadError(`Error uploading block chunk (Status ${xhr.status})`, xhr.status));
      }
    };

    xhr.onerror = () => reject(new ChunkUploadError("Network error during chunk upload.", 0));
    xhr.ontimeout = () => reject(new ChunkUploadError("Chunk upload timed out.", 0));
    xhr.send(chunk);
  });
}

class ChunkUploadError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
    this.name = "ChunkUploadError";
  }
}

async function uploadChunkWithRetry(
  chunk: Blob,
  getBlockUrl: () => string,
  getUploadUrl: () => string,
  refreshAuthorization: (rejectedUrl: string) => Promise<void>
): Promise<void> {
  let refreshedAfterForbidden = false;
  for (let attempt = 0; ; attempt++) {
    const uploadUrl = getUploadUrl();
    const blockUrl = getBlockUrl();
    try {
      await uploadChunk(chunk, blockUrl);
      return;
    } catch (error) {
      if (error instanceof ChunkUploadError && error.status === 403 && !refreshedAfterForbidden) {
        refreshedAfterForbidden = true;
        await refreshAuthorization(uploadUrl);
        continue;
      }
      const retryable = !(error instanceof ChunkUploadError) ||
        error.status === 0 || error.status === 408 || error.status === 429 || error.status >= 500;

      if (!retryable || attempt >= MAX_CHUNK_RETRIES) throw error;
      await new Promise((resolve) => setTimeout(resolve, 500 * 2 ** attempt));
    }
  }
}

/**
 * Ensambla la lista completa de bloques en Azure Blob Storage
 */
async function commitBlockListWithRetry(
  getUploadUrl: () => string,
  blockIds: string[],
  contentType: string,
  refreshAuthorization: (rejectedUrl: string) => Promise<void>
): Promise<void> {
  let refreshedAfterForbidden = false;
  for (let attempt = 0; ; attempt++) {
    const uploadUrl = getUploadUrl();
    try {
      await commitBlockList(uploadUrl, blockIds, contentType);
      return;
    } catch (error) {
      if (error instanceof ChunkUploadError && error.status === 403 && !refreshedAfterForbidden) {
        refreshedAfterForbidden = true;
        await refreshAuthorization(uploadUrl);
        continue;
      }
      const retryable = !(error instanceof ChunkUploadError) ||
        error.status === 0 || error.status === 408 || error.status === 429 || error.status >= 500;
      if (!retryable || attempt >= MAX_CHUNK_RETRIES) throw error;
      await new Promise((resolve) => setTimeout(resolve, 500 * 2 ** attempt));
    }
  }
}

function commitBlockList(uploadUrl: string, blockIds: string[], contentType: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const separator = uploadUrl.includes("?") ? "&" : "?";
    const commitUrl = `${uploadUrl}${separator}comp=blocklist`;

    // XML requerido por la API REST de Azure para ordenar los bloques
    let xmlPayload = '<?xml version="1.0" encoding="utf-8"?><BlockList>';
    for (const id of blockIds) {
      xmlPayload += `<Latest>${id}</Latest>`;
    }
    xmlPayload += "</BlockList>";

    const xhr = new XMLHttpRequest();
    xhr.open("PUT", commitUrl, true);
    xhr.setRequestHeader("Content-Type", "application/xml");
    if (contentType) {
      xhr.setRequestHeader("x-ms-blob-content-type", contentType);
    }

    xhr.onload = () => {
      if (xhr.status === 200 || xhr.status === 201) {
        resolve();
      } else {
        reject(new ChunkUploadError(`Failed to commit block list in Azure (Status ${xhr.status})`, xhr.status));
      }
    };

    xhr.onerror = () => reject(new ChunkUploadError("Network error committing block list.", 0));
    xhr.send(xmlPayload);
  });
}
