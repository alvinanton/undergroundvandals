// FILE: src/services/mediaService.ts

interface UploadAssetInput {
  url: string;
  publicId: string;
  type: 'image' | 'video';
}

interface CreateMediaPayload {
  title: string;
  description?: string;
  category: string;
  hashtags?: string[];
  assets: UploadAssetInput[];
}

/**
 * Uploads a file directly to Cloudinary using a backend-generated signature,
 * then registers the resulting metadata in the ASP.NET Core backend.
 */
export async function uploadMediaItem(
  file: File,
  metadata: { title: string; description?: string; category: string; hashtags?: string[] },
  authToken: string,
  apiBaseUrl: string = 'http://localhost:5098' // Replace with your production URL when deployed
) {
  try {
    // Determine resource type based on file MIME type
    const isVideo = file.type.startsWith('video/');
    const folder = isVideo ? 'underground_vandals/videos' : 'underground_vandals/photos';

    // 1. Fetch upload signature and parameters from your backend
    const sigRes = await fetch(`${apiBaseUrl}/api/media/upload-signature?folder=${folder}`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${authToken}`
      }
    });

    if (!sigRes.ok) {
      throw new Error('Failed to retrieve upload authorization signature from backend.');
    }

    const sigData = await sigRes.json();
    // sigData contains: timestamp, signature, cloudName, apiKey, folder

    // 2. Prepare FormData payload for direct Cloudinary upload
    const formData = new FormData();
    formData.append('file', file);
    formData.append('api_key', sigData.apiKey);
    formData.append('timestamp', sigData.timestamp.toString());
    formData.append('signature', sigData.signature);
    formData.append('folder', sigData.folder);

    // 3. Upload directly to Cloudinary (bypassing backend memory completely)
    const resourceType = isVideo ? 'video' : 'image';
    const cloudinaryUrl = `https://api.cloudinary.com/v1_1/${sigData.cloudName}/${resourceType}/upload`;

    const cloudRes = await fetch(cloudinaryUrl, {
      method: 'POST',
      body: formData
    });

    if (!cloudRes.ok) {
      const errorResponse = await cloudRes.json();
      throw new Error(`Cloudinary upload failed: ${errorResponse.error?.message || 'Unknown error'}`);
    }

    const cloudData = await cloudRes.json();
    // cloudData contains secure_url, public_id, etc.

    // 4. Send lightweight JSON metadata payload to your backend to save in PostgreSQL
    const payload: CreateMediaPayload = {
      title: metadata.title,
      description: metadata.description,
      category: metadata.category,
      hashtags: metadata.hashtags,
      assets: [
        {
          url: cloudData.secure_url,
          publicId: cloudData.public_id,
          type: isVideo ? 'video' : 'image'
        }
      ]
    };

    const finalRes = await fetch(`${apiBaseUrl}/api/media/upload`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${authToken}`
      },
      body: JSON.stringify(payload)
    });

    if (!finalRes.ok) {
      throw new Error('Failed to register uploaded asset metadata in the backend database.');
    }

    return await finalRes.json();
  } catch (error) {
    console.error('Error during media upload process:', error);
    throw error;
  }
}