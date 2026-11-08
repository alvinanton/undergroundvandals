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
}

const CHUNK_SIZE = 8 * 1024 * 1024; // 8 MB por trozo

/**
 * Uploads a file directly to Azure Blob Storage using a SAS URL.
 * Automatically optimizes images to WebP and switches between single-blob
 * upload and chunked block upload based on processed file size.
 */
export async function uploadFileToAzure(
  file: File,
  targetPath: string,
  token: string,
  apiUrl: string = import.meta.env.PUBLIC_API_URL || "http://localhost:5098",
  onProgress?: (percentage: number) => void
): Promise<AzureUploadResult> {
  // 0. Optimización en cliente: Si es imagen, comprimir a WebP transparente en navegador
  let fileToUpload = file;
  if (file.type.startsWith("image/")) {
    fileToUpload = await compressImageToWebP(file);
  }

  // 1. Solicitar firma SAS al backend
  const encodedPath = encodeURIComponent(targetPath);
  const sigRes = await fetch(`${apiUrl}/api/media/upload-signature?target=${encodedPath}`, {
    method: "GET",
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!sigRes.ok) {
    throw new Error("Failed to obtain upload authorization for Azure Blob Storage.");
  }

  const sasData: SasResponse = await sigRes.json();

  // 2. Elegir estrategia según el tamaño del archivo procesado
  if (fileToUpload.size <= CHUNK_SIZE) {
    return uploadSingleBlob(fileToUpload, sasData, onProgress);
  } else {
    return uploadBlobInChunks(fileToUpload, sasData, onProgress);
  }
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
  file: File,
  sasData: SasResponse,
  onProgress?: (percentage: number) => void
): Promise<AzureUploadResult> {
  const totalChunks = Math.ceil(file.size / CHUNK_SIZE);
  const blockIds: string[] = [];

  for (let i = 0; i < totalChunks; i++) {
    const start = i * CHUNK_SIZE;
    const end = Math.min(start + CHUNK_SIZE, file.size);
    const chunk = file.slice(start, end);

    // ID del bloque codificado en Base64 de longitud fija (e.g., "block-000000")
    const blockId = btoa(`block-${String(i).padStart(6, "0")}`);
    blockIds.push(blockId);

    // Construir la URL del bloque agregando comp=block y blockid
    const separator = sasData.uploadUrl.includes("?") ? "&" : "?";
    const blockUrl = `${sasData.uploadUrl}${separator}comp=block&blockid=${encodeURIComponent(blockId)}`;

    await uploadChunk(chunk, blockUrl);

    if (onProgress) {
      const progress = Math.round(((i + 1) / totalChunks) * 100);
      onProgress(progress);
    }
  }

  // Confirmar y ensamblar la lista de bloques en Azure
  await commitBlockList(sasData.uploadUrl, blockIds, file.type);

  return {
    url: sasData.fileUrl,
    publicId: sasData.publicId,
  };
}

/**
 * Envía un trozo individual mediante PUT
 */
function uploadChunk(chunk: Blob, blockUrl: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("PUT", blockUrl, true);

    xhr.onload = () => {
      if (xhr.status === 200 || xhr.status === 201) {
        resolve();
      } else {
        reject(new Error(`Error uploading block chunk (Status ${xhr.status})`));
      }
    };

    xhr.onerror = () => reject(new Error("Network error during chunk upload."));
    xhr.send(chunk);
  });
}

/**
 * Ensambla la lista completa de bloques en Azure Blob Storage
 */
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
        reject(new Error(`Failed to commit block list in Azure (Status ${xhr.status})`));
      }
    };

    xhr.onerror = () => reject(new Error("Network error committing block list."));
    xhr.send(xmlPayload);
  });
}